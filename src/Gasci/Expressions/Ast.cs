namespace Gasci.Expressions;

internal abstract record Node(int Position);
internal sealed record IntLiteral(int Value, int Position) : Node(Position);
internal sealed record StringLiteral(string Value, int Position) : Node(Position);
internal sealed record BoolLiteral(bool Value, int Position) : Node(Position);
internal sealed record VariableNode(string Name, int Position) : Node(Position);
internal sealed record UnaryNode(string Op, Node Operand, int Position) : Node(Position);
internal sealed record BinaryNode(string Op, Node Left, Node Right, int Position) : Node(Position);
internal sealed record TernaryNode(Node Condition, Node WhenTrue, Node WhenFalse, int Position) : Node(Position);
internal sealed record CallNode(string Function, List<Node> Arguments, int Position) : Node(Position);
