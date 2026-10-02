using System.Text.Json;
using Relato.Core;
using Relato.Variables;

namespace Relato.Tests;

[Collection("registry")]
public sealed class VariableTests
{
    public VariableTests() => TestContent.LoadVariables(TestContent.Game);

    [Fact]
    public void IntsAreClampedAtRuntime()
    {
        VariableRegistry.Set("game.courage", Value.Of(25));
        Assert.Equal(10, VariableRegistry.GetInt("game.courage"));
        VariableRegistry.Add("game.courage", -50);
        Assert.Equal(0, VariableRegistry.GetInt("game.courage"));
    }

    [Fact]
    public void InvalidStringsAreRejected()
    {
        Assert.False(VariableRegistry.Set("game.name", Value.Of("Bartholomew")));
        Assert.Equal("Ana", VariableRegistry.GetString("game.name"));
    }

    [Fact]
    public void WrongTypeIsIgnored()
    {
        Assert.False(VariableRegistry.Set("game.courage", Value.Of("high")));
        Assert.Equal(0, VariableRegistry.GetInt("game.courage"));
    }

    [Fact]
    public void LoadedValuesOutOfRulesResetToDefault()
    {
        var values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("""{ "courage": 99, "gold": "x", "name": "Eva", "removed": 1 }""")!;
        VariableRegistry.LoadValues(VarScope.Game, values, "test");
        Assert.Equal(0, VariableRegistry.GetInt("game.courage"));   // out of range → default
        Assert.Equal(50, VariableRegistry.GetInt("game.gold"));     // wrong type → default
        Assert.Equal("Eva", VariableRegistry.GetString("game.name"));
    }

    [Fact]
    public void ResetRestoresDefaults()
    {
        VariableRegistry.Set("game.gold", Value.Of(3));
        VariableRegistry.Reset(VarScope.Game);
        Assert.Equal(50, VariableRegistry.GetInt("game.gold"));
    }

    [Fact]
    public void UndeclaredVariablesThrow() =>
        Assert.Throws<ContentException>(() => VariableRegistry.Get("game.nothing"));

    [Theory]
    [InlineData("""{ "x": { "type": "int" } }""")]                                   // no default
    [InlineData("""{ "x": { "type": "int", "default": "a" } }""")]                   // wrong default type
    [InlineData("""{ "x": { "type": "int", "default": 20, "max": 10 } }""")]         // default breaks the rules
    [InlineData("""{ "x": { "type": "bool", "default": true, "max": 3 } }""")]       // max on a bool
    [InlineData("""{ "x": { "type": "int", "default": 1, "bind": "audio.music" } }""")] // binding outside settings
    public void BrokenTemplatesStopTheLoad(string template) =>
        Assert.Throws<ContentException>(() => TestContent.LoadVariables(template));

    [Fact]
    public void BindingsAreCalledOnChange()
    {
        TestContent.LoadVariables("{}", """{ "music": { "type": "int", "default": 5, "min": 0, "max": 10, "bind": "audio.music" } }""");
        int received = -1;
        Bindings.Install(Bindings.MusicVolume, (_, v) => received = v.Int);
        VariableRegistry.Set("settings.music", Value.Of(8));
        Assert.Equal(8, received);
    }

    [Fact]
    public void SnapshotUsesShortNames()
    {
        Dictionary<string, object> snapshot = VariableRegistry.Snapshot(VarScope.Game);
        Assert.Equal(50, snapshot["gold"]);
        Assert.Equal(false, snapshot["met_shadow"]);
    }
}
