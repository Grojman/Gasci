using Gasci.Expressions;
using Gasci.Variables;

namespace Gasci.Tests;

[Collection("registry")]
public sealed class ExpressionTests
{
    public ExpressionTests() => TestContent.LoadVariables(TestContent.Game);

    [Theory]
    [InlineData("1 + 2 * 3", 7)]
    [InlineData("(1 + 2) * 3", 9)]
    [InlineData("10 / 3", 3)]
    [InlineData("10 % 3", 1)]
    [InlineData("-game.gold + 5", -45)]
    [InlineData("max(game.courage, 4)", 4)]
    [InlineData("clamp(game.gold, 0, 20)", 20)]
    [InlineData("len(game.name)", 3)]
    [InlineData("game.met_shadow ? 1 : 2", 2)]
    public void Arithmetic(string source, int expected) => Assert.Equal(expected, Expression.Compile(source).EvalInt());

    [Theory]
    [InlineData("game.courage >= 0 && !game.met_shadow", true)]
    [InlineData("game.gold > 10 || game.met_shadow", true)]
    [InlineData("game.name == 'Ana'", true)]
    [InlineData("game.name != 'Ana'", false)]
    [InlineData("'abc' < 'abd'", true)]
    [InlineData("contains(game.name, 'n')", true)]
    [InlineData("startsWith(lower(game.name), 'an')", true)]
    public void Conditions(string source, bool expected) => Assert.Equal(expected, Expression.Condition(source).EvalBool());

    [Fact]
    public void StringConcatenation() =>
        Assert.Equal("Ana has 50", Expression.Compile("game.name + ' has ' + game.gold").EvalString());

    [Fact]
    public void ReadsCurrentValues()
    {
        Expression e = Expression.Condition("game.courage >= 2");
        Assert.False(e.EvalBool());
        VariableRegistry.Set("game.courage", Value.Of(2));
        Assert.True(e.EvalBool());
        Assert.Equal(["game.courage"], e.Variables);
    }

    [Theory]
    [InlineData("game.courage && true")]         // ints are not booleans
    [InlineData("!game.courage")]
    [InlineData("game.courage == 'x'")]          // int vs string
    [InlineData("game.unknown > 1")]             // undeclared
    [InlineData("courage > 1")]                  // no scope prefix
    [InlineData("1 < 2 < 3")]                    // chained comparison
    [InlineData("game.courage >")]               // syntax
    [InlineData("'unterminated")]
    [InlineData("nope(1)")]
    [InlineData("game.met_shadow ? 1 : 'a'")]
    public void CompileErrors(string source) => Assert.Throws<ExpressionException>(() => Expression.Compile(source));

    [Fact]
    public void ConditionsMustBeBoolWithoutRandom()
    {
        Assert.Throws<ExpressionException>(() => Expression.Condition("game.gold + 1"));
        Assert.Throws<ExpressionException>(() => Expression.Condition("rand(1, 6) > 3"));
        Expression roll = Expression.Compile("rand(1, 6)");
        Assert.True(roll.UsesRandom);
        Assert.InRange(roll.EvalInt(), 1, 6);
    }

    [Fact]
    public void DivisionByZeroThrows() =>
        Assert.Throws<ExpressionException>(() => Expression.Compile("game.gold / game.courage").EvalInt());

    [Fact]
    public void EmptyConditionIsTrue() => Assert.True(Expression.IsTrue("  "));
}
