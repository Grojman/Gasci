using Relato.Variables;

namespace Relato.Expressions;

/// <summary>A node after type checking: exactly one of the delegates is set, matching <see cref="Type"/>.</summary>
internal sealed record Typed(VarType Type, Func<int>? I = null, Func<bool>? B = null, Func<string>? S = null)
{
    public static Typed Int(Func<int> f) => new(VarType.Int, I: f);
    public static Typed Bool(Func<bool> f) => new(VarType.Bool, B: f);
    public static Typed Str(Func<string> f) => new(VarType.String, S: f);

    /// <summary>Text of any value (used by string concatenation).</summary>
    public Func<string> AsText() => Type switch
    {
        VarType.String => S!,
        VarType.Int => () => I!().ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => () => B!() ? "true" : "false",
    };
}

/// <summary>
/// Type-checks the syntax tree against the declared variables and turns it into closures that read
/// the registry directly. Ints are no longer booleans: !, &amp;&amp;, || and ?: need bool operands.
/// </summary>
internal sealed class Compiler(string source)
{
    public readonly HashSet<string> Variables = new();
    public bool UsesRandom;

    private ExpressionException Error(Node node, string message) => new(source, node.Position, message);

    private static string Name(VarType t) => Value.TypeName(t);

    public Typed Compile(Node node) => node switch
    {
        IntLiteral n => Constant(n.Value),
        StringLiteral n => Constant(n.Value),
        BoolLiteral n => Constant(n.Value),
        VariableNode n => Variable(n),
        UnaryNode n => Unary(n),
        BinaryNode n => Binary(n),
        TernaryNode n => Ternary(n),
        CallNode n => Call(n),
        _ => throw Error(node, "unsupported expression"),
    };

    private static Typed Constant(int v) => Typed.Int(() => v);
    private static Typed Constant(string v) => Typed.Str(() => v);
    private static Typed Constant(bool v) => Typed.Bool(() => v);

    private Typed Variable(VariableNode n)
    {
        VariableDefinition? def = VariableRegistry.Find(n.Name);
        if (def is null) throw Error(n, VariableRegistry.Undeclared(n.Name));
        Variables.Add(n.Name);
        return def.Type switch
        {
            VarType.Int => Typed.Int(() => VariableRegistry.Read(def).Int),
            VarType.Bool => Typed.Bool(() => VariableRegistry.Read(def).Bool),
            _ => Typed.Str(() => VariableRegistry.Read(def).Str),
        };
    }

    private Typed Unary(UnaryNode n)
    {
        Typed operand = Compile(n.Operand);
        if (n.Op == "!")
        {
            Expect(n.Operand, operand, VarType.Bool, "'!'");
            Func<bool> f = operand.B!;
            return Typed.Bool(() => !f());
        }
        Expect(n.Operand, operand, VarType.Int, "unary '-'");
        Func<int> g = operand.I!;
        return Typed.Int(() => checked(-g()));
    }

    private void Expect(Node node, Typed typed, VarType type, string what)
    {
        if (typed.Type != type)
            throw Error(node, type == VarType.Bool && typed.Type == VarType.Int
                ? $"{what} needs a bool, found an int (compare it, e.g. 'x > 0')"
                : $"{what} needs a {Name(type)}, found a {Name(typed.Type)}");
    }

    private Typed Binary(BinaryNode n)
    {
        Typed l = Compile(n.Left), r = Compile(n.Right);
        switch (n.Op)
        {
            case "&&" or "||":
            {
                Expect(n.Left, l, VarType.Bool, $"'{n.Op}'");
                Expect(n.Right, r, VarType.Bool, $"'{n.Op}'");
                Func<bool> a = l.B!, b = r.B!;
                return n.Op == "&&" ? Typed.Bool(() => a() && b()) : Typed.Bool(() => a() || b());
            }
            case "==" or "!=":
            {
                if (l.Type != r.Type) throw Error(n, $"cannot compare a {Name(l.Type)} with a {Name(r.Type)}");
                bool eq = n.Op == "==";
                return l.Type switch
                {
                    VarType.Int => Typed.Bool(() => (l.I!() == r.I!()) == eq),
                    VarType.Bool => Typed.Bool(() => (l.B!() == r.B!()) == eq),
                    _ => Typed.Bool(() => string.Equals(l.S!(), r.S!(), StringComparison.Ordinal) == eq),
                };
            }
            case "<" or "<=" or ">" or ">=":
            {
                if (l.Type != r.Type || l.Type == VarType.Bool)
                    throw Error(n, $"'{n.Op}' compares two ints or two strings, not a {Name(l.Type)} and a {Name(r.Type)}");
                Func<int> cmp = l.Type == VarType.Int
                    ? () => l.I!().CompareTo(r.I!())
                    : () => string.CompareOrdinal(l.S!(), r.S!());
                return n.Op switch
                {
                    "<" => Typed.Bool(() => cmp() < 0),
                    "<=" => Typed.Bool(() => cmp() <= 0),
                    ">" => Typed.Bool(() => cmp() > 0),
                    _ => Typed.Bool(() => cmp() >= 0),
                };
            }
            case "+" when l.Type == VarType.String || r.Type == VarType.String:
            {
                if (l.Type == VarType.Bool || r.Type == VarType.Bool) throw Error(n, "cannot add a bool to a string");
                Func<string> a = l.AsText(), b = r.AsText();
                return Typed.Str(() => a() + b());
            }
            default:
            {
                Expect(n.Left, l, VarType.Int, $"'{n.Op}'");
                Expect(n.Right, r, VarType.Int, $"'{n.Op}'");
                Func<int> a = l.I!, b = r.I!;
                string src = source;
                return n.Op switch
                {
                    "+" => Typed.Int(() => checked(a() + b())),
                    "-" => Typed.Int(() => checked(a() - b())),
                    "*" => Typed.Int(() => checked(a() * b())),
                    "/" => Typed.Int(() => { int d = b(); return d == 0 ? throw new ExpressionException(src, "division by zero") : a() / d; }),
                    _ => Typed.Int(() => { int d = b(); return d == 0 ? throw new ExpressionException(src, "modulo by zero") : a() % d; }),
                };
            }
        }
    }

    private Typed Ternary(TernaryNode n)
    {
        Typed c = Compile(n.Condition), t = Compile(n.WhenTrue), f = Compile(n.WhenFalse);
        Expect(n.Condition, c, VarType.Bool, "the condition of '?:'");
        if (t.Type != f.Type) throw Error(n, $"both sides of '?:' must have the same type ({Name(t.Type)} / {Name(f.Type)})");
        Func<bool> cond = c.B!;
        return t.Type switch
        {
            VarType.Int => Typed.Int(() => cond() ? t.I!() : f.I!()),
            VarType.Bool => Typed.Bool(() => cond() ? t.B!() : f.B!()),
            _ => Typed.Str(() => cond() ? t.S!() : f.S!()),
        };
    }

    /// <summary>Signatures of the built-in functions.</summary>
    public static readonly IReadOnlyDictionary<string, (VarType[] args, VarType result)> Functions =
        new Dictionary<string, (VarType[], VarType)>
        {
            ["min"] = ([VarType.Int, VarType.Int], VarType.Int),
            ["max"] = ([VarType.Int, VarType.Int], VarType.Int),
            ["abs"] = ([VarType.Int], VarType.Int),
            ["clamp"] = ([VarType.Int, VarType.Int, VarType.Int], VarType.Int),
            ["len"] = ([VarType.String], VarType.Int),
            ["lower"] = ([VarType.String], VarType.String),
            ["upper"] = ([VarType.String], VarType.String),
            ["contains"] = ([VarType.String, VarType.String], VarType.Bool),
            ["startsWith"] = ([VarType.String, VarType.String], VarType.Bool),
            ["endsWith"] = ([VarType.String, VarType.String], VarType.Bool),
            ["text"] = ([VarType.String], VarType.String),
            ["rand"] = ([VarType.Int, VarType.Int], VarType.Int),
        };

    private Typed Call(CallNode n)
    {
        if (!Functions.TryGetValue(n.Function, out var signature))
            throw Error(n, $"unknown function '{n.Function}' (known: {string.Join(", ", Functions.Keys)})");
        if (n.Arguments.Count != signature.args.Length)
            throw Error(n, $"{n.Function}() takes {signature.args.Length} argument(s), got {n.Arguments.Count}");
        var args = new Typed[n.Arguments.Count];
        for (int i = 0; i < args.Length; i++)
        {
            args[i] = Compile(n.Arguments[i]);
            Expect(n.Arguments[i], args[i], signature.args[i], $"argument {i + 1} of {n.Function}()");
        }

        Func<int> I(int i) => args[i].I!;
        Func<string> S(int i) => args[i].S!;
        switch (n.Function)
        {
            case "min": { var a = I(0); var b = I(1); return Typed.Int(() => Math.Min(a(), b())); }
            case "max": { var a = I(0); var b = I(1); return Typed.Int(() => Math.Max(a(), b())); }
            case "abs": { var a = I(0); return Typed.Int(() => checked(Math.Abs(a()))); }
            case "clamp":
            {
                var v = I(0); var lo = I(1); var hi = I(2);
                return Typed.Int(() => { int l = lo(), h = hi(); return l > h ? l : Math.Clamp(v(), l, h); });
            }
            case "len": { var s = S(0); return Typed.Int(() => s().Length); }
            case "lower": { var s = S(0); return Typed.Str(() => s().ToLowerInvariant()); }
            case "upper": { var s = S(0); return Typed.Str(() => s().ToUpperInvariant()); }
            case "contains": { var s = S(0); var p = S(1); return Typed.Bool(() => s().Contains(p(), StringComparison.Ordinal)); }
            case "startsWith": { var s = S(0); var p = S(1); return Typed.Bool(() => s().StartsWith(p(), StringComparison.Ordinal)); }
            case "endsWith": { var s = S(0); var p = S(1); return Typed.Bool(() => s().EndsWith(p(), StringComparison.Ordinal)); }
            case "text": { var s = S(0); return Typed.Str(() => Expression.TextLookup(s())); }
            default:
            {
                UsesRandom = true;
                var lo = I(0); var hi = I(1);
                return Typed.Int(() => { int l = lo(), h = hi(); return h < l ? l : Expression.Random.Next(l, h + 1); });
            }
        }
    }
}
