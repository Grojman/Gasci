using Relato.Variables;

namespace Relato.Expressions;

/// <summary>
/// A compiled, typed expression: conditions ("game.courage >= 2 &amp;&amp; !game.met_shadow"), values of
/// actions ("game.gold - 10"), and block properties. Compiled once and cached by its text.
/// Language: ints, bools and 'strings'; + - * / % (integer), comparisons, ! &amp;&amp; || and c ? a : b;
/// string + concatenates; functions min, max, abs, clamp, len, lower, upper, contains, startsWith,
/// endsWith, text(key) and rand(lo, hi) (rand only in action values, never in conditions).
/// </summary>
public sealed class Expression
{
    private static readonly Dictionary<string, Expression> Cache = new();

    /// <summary>Translates a text key for text(); installed by the game (defaults to the key itself).</summary>
    public static Func<string, string> TextLookup { get; set; } = key => key;
    public static Random Random { get; set; } = new();

    private readonly Typed _compiled;

    public string Source { get; }
    public VarType Type => _compiled.Type;
    /// <summary>Variables the expression reads (for dependency tracking and validation).</summary>
    public IReadOnlySet<string> Variables { get; }
    public bool UsesRandom { get; }

    private Expression(string source, Typed compiled, IReadOnlySet<string> variables, bool usesRandom)
    {
        Source = source;
        _compiled = compiled;
        Variables = variables;
        UsesRandom = usesRandom;
    }

    /// <summary>Compiles (or returns the cached) expression. Throws <see cref="ExpressionException"/>.</summary>
    public static Expression Compile(string source)
    {
        if (Cache.TryGetValue(source, out Expression? cached)) return cached;
        var compiler = new Compiler(source);
        Typed typed = compiler.Compile(Parser.Parse(source));
        return Cache[source] = new Expression(source, typed, compiler.Variables, compiler.UsesRandom);
    }

    /// <summary>Compiles a condition: it must be bool and must not use rand().</summary>
    public static Expression Condition(string source)
    {
        Expression e = Compile(source);
        if (e.Type != VarType.Bool)
            throw new ExpressionException(source, $"a condition must be a bool, this is a {Value.TypeName(e.Type)}");
        if (e.UsesRandom)
            throw new ExpressionException(source, "rand() is not allowed in conditions (they are re-evaluated and would flicker)");
        return e;
    }

    /// <summary>Empty conditions are true.</summary>
    public static bool IsTrue(string? condition) =>
        string.IsNullOrWhiteSpace(condition) || Condition(condition).EvalBool();

    /// <summary>Forgets compiled expressions (after variables are reloaded).</summary>
    public static void ClearCache() => Cache.Clear();

    public bool EvalBool() => _compiled.B is { } f ? f() : throw new ExpressionException(Source, "is not a bool");
    public int EvalInt() => _compiled.I is { } f ? f() : throw new ExpressionException(Source, "is not an int");
    public string EvalString() => _compiled.AsText()();

    public Value Evaluate() => Type switch
    {
        VarType.Int => Value.Of(EvalInt()),
        VarType.Bool => Value.Of(EvalBool()),
        _ => Value.Of(_compiled.S!()),
    };
}
