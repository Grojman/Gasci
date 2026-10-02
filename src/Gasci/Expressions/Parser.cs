namespace Gasci.Expressions;

/// <summary>
/// Recursive descent parser. Precedence, lowest first:
///   ?:   ||   &amp;&amp;   == !=   &lt; &lt;= &gt; &gt;=   + -   * / %   unary ! -
/// </summary>
internal sealed class Parser(List<Token> tokens, string source)
{
    private int _pos;

    private Token Peek => tokens[_pos];

    public static Node Parse(string source)
    {
        var parser = new Parser(Lexer.Tokenize(source), source);
        Node node = parser.Ternary();
        if (parser.Peek.Kind != TokenKind.End) throw parser.Error($"unexpected '{parser.Peek.Text}'");
        return node;
    }

    private ExpressionException Error(string message) => new(source, Peek.Position, message);

    private bool AcceptOp(string op)
    {
        if (Peek.Kind != TokenKind.Op || Peek.Text != op) return false;
        _pos++;
        return true;
    }

    private void ExpectOp(string op)
    {
        if (!AcceptOp(op)) throw Error(Peek.Kind == TokenKind.End ? $"missing '{op}'" : $"expected '{op}' but found '{Peek.Text}'");
    }

    private Node Ternary()
    {
        Node condition = Or();
        if (Peek.Kind != TokenKind.Op || Peek.Text != "?") return condition;
        int at = Peek.Position;
        _pos++;
        Node whenTrue = Ternary();
        ExpectOp(":");
        Node whenFalse = Ternary();
        return new TernaryNode(condition, whenTrue, whenFalse, at);
    }

    private Node Binary(Func<Node> next, params string[] ops)
    {
        Node left = next();
        while (Peek.Kind == TokenKind.Op && ops.Contains(Peek.Text))
        {
            Token op = tokens[_pos++];
            left = new BinaryNode(op.Text, left, next(), op.Position);
        }
        return left;
    }

    private Node Or() => Binary(And, "||");
    private Node And() => Binary(Equality, "&&");
    private Node Equality() => Binary(Relational, "==", "!=");

    private Node Relational()
    {
        Node left = Additive();
        if (Peek.Kind == TokenKind.Op && Peek.Text is "<" or "<=" or ">" or ">=")
        {
            Token op = tokens[_pos++];
            left = new BinaryNode(op.Text, left, Additive(), op.Position);
            if (Peek.Kind == TokenKind.Op && Peek.Text is "<" or "<=" or ">" or ">=")
                throw Error("comparisons cannot be chained; use &&");
        }
        return left;
    }

    private Node Additive() => Binary(Multiplicative, "+", "-");
    private Node Multiplicative() => Binary(Unary, "*", "/", "%");

    private Node Unary()
    {
        if (Peek.Kind == TokenKind.Op && Peek.Text is "!" or "-")
        {
            Token op = tokens[_pos++];
            return new UnaryNode(op.Text, Unary(), op.Position);
        }
        return Primary();
    }

    private Node Primary()
    {
        Token t = Peek;
        switch (t.Kind)
        {
            case TokenKind.Int:
                _pos++;
                if (!int.TryParse(t.Text, out int value)) throw new ExpressionException(source, t.Position, $"number {t.Text} is too big");
                return new IntLiteral(value, t.Position);
            case TokenKind.String:
                _pos++;
                return new StringLiteral(t.Text, t.Position);
            case TokenKind.Name:
                _pos++;
                if (t.Text == "true") return new BoolLiteral(true, t.Position);
                if (t.Text == "false") return new BoolLiteral(false, t.Position);
                if (!AcceptOp("(")) return new VariableNode(t.Text, t.Position);
                var args = new List<Node>();
                if (!AcceptOp(")"))
                {
                    do args.Add(Ternary()); while (AcceptOp(","));
                    ExpectOp(")");
                }
                return new CallNode(t.Text, args, t.Position);
            case TokenKind.Op when t.Text == "(":
                _pos++;
                Node inner = Ternary();
                ExpectOp(")");
                return inner;
            case TokenKind.End:
                throw Error("unexpected end of expression");
            default:
                throw Error($"unexpected '{t.Text}'");
        }
    }
}
