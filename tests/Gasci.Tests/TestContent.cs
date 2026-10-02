using Gasci.Core;
using Gasci.Expressions;
using Gasci.Variables;

namespace Gasci.Tests;

/// <summary>Writes small variable templates to a temporary folder and loads them into the registry.</summary>
internal static class TestContent
{
    public static readonly object Lock = new();

    public static string Folder()
    {
        string folder = Path.Combine(Path.GetTempPath(), "gasci-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    public static void LoadVariables(string game, string settings = "{}", string session = "{}")
    {
        Log.FilePath = null;
        string folder = Folder();
        File.WriteAllText(Path.Combine(folder, "game.json"), game);
        File.WriteAllText(Path.Combine(folder, "settings.json"), settings);
        File.WriteAllText(Path.Combine(folder, "session.json"), session);
        Bindings.Clear();
        VariableRegistry.Load(folder, ["es", "en"]);
        Expression.ClearCache();
    }

    public const string Game = """
    {
      "courage":    { "type": "int", "default": 0, "min": 0, "max": 10 },
      "gold":       { "type": "int", "default": 50 },
      "met_shadow": { "type": "bool", "default": false },
      "name":       { "type": "string", "default": "Ana", "maxLength": 5 }
    }
    """;
}

[CollectionDefinition("registry", DisableParallelization = true)]
public sealed class RegistryCollection;
