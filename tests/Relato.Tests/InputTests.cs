using Relato.Core;
using Relato.Input;
using Relato.Variables;
using SadConsole.Input;

namespace Relato.Tests;

[Collection("registry")]
public sealed class InputTests
{
    private const string Actions = """
    {
      "ui.up": { "keys": ["Up", "W"], "context": "ui" }, "ui.down": { "keys": ["Down"], "context": "ui" },
      "ui.left": { "keys": ["Left"], "context": "ui" }, "ui.right": { "keys": ["Right"], "context": "ui" },
      "ui.confirm": { "keys": ["Space"], "context": "ui" }, "ui.back": { "keys": ["Escape"], "context": "ui" },
      "map.up": { "keys": ["Up", "W"], "context": "map" }, "map.down": { "keys": ["Down"], "context": "map" },
      "map.left": { "keys": ["Left"], "context": "map" }, "map.right": { "keys": ["Right"], "context": "map" },
      "map.interact": { "keys": ["E"], "context": "map" }, "inventory": { "keys": ["I"], "context": "map" }
    }
    """;

    private static void Load(string json = Actions)
    {
        string file = Path.Combine(TestContent.Folder(), "actions.json");
        File.WriteAllText(file, json);
        InputMap.Load(file, 2);
    }

    public InputTests()
    {
        Log.FilePath = null;
        Load();
    }

    [Fact]
    public void SameKeyInDifferentContextsIsAllowed() => Assert.Equal(Keys.W, InputMap.KeyAt("map.up", 1));

    [Fact]
    public void RebindingAKeyInUseSwaps()
    {
        string? swapped = InputMap.Bind("inventory", 0, Keys.E);
        Assert.Equal("map.interact", swapped);
        Assert.Equal(Keys.E, InputMap.KeyAt("inventory", 0));
        Assert.Equal(Keys.I, InputMap.KeyAt("map.interact", 0));
        Assert.Empty(InputMap.Conflicts());
    }

    [Fact]
    public void ResetRestoresDefaults()
    {
        InputMap.Bind("inventory", 0, Keys.E);
        InputMap.Reset("inventory");
        Assert.Equal(Keys.I, InputMap.KeyAt("inventory", 0));
        Assert.Equal(Keys.E, InputMap.KeyAt("map.interact", 0));
    }

    [Fact]
    public void EnterCannotBeBound()
    {
        Assert.Throws<ArgumentException>(() => InputMap.Bind("inventory", 0, Keys.Enter));
        Assert.Throws<ContentException>(() => Load(Actions.Replace("\"keys\": [\"I\"]", "\"keys\": [\"Enter\"]")));
    }

    [Fact]
    public void ConflictsInDefaultsAreRejected() =>
        Assert.Throws<ContentException>(() => Load(Actions.Replace("\"keys\": [\"I\"]", "\"keys\": [\"E\"]")));

    [Fact]
    public void MissingEngineActionsAreRejected() =>
        Assert.Throws<ContentException>(() => Load("""{ "ui.up": { "keys": ["Up"] } }"""));

    [Fact]
    public void OverridesAreApplied()
    {
        PersistenceScheduler.DelaySeconds = 100;
        string file = Path.Combine(TestContent.Folder(), "keybindings.json");
        File.WriteAllText(file, """{ "inventory": ["Tab", null] }""");
        InputMap.LoadOverrides(file);
        Assert.Equal(Keys.Tab, InputMap.KeyAt("inventory", 0));
    }
}
