using System.Text.Json;
using System.Text.Json.Nodes;

namespace Gasci.Core;

/// <summary>
/// Everything needed to restore a game exactly as it was: the scenes marked "saved" in the stack
/// (with their parameters), the state of their blocks (map and player position...), the game-scope
/// variables and the events already completed or waiting to run. Settings are stored separately.
/// </summary>
public sealed class SaveGame
{
    public List<SavedScene> Scenes { get; set; } = new();
    /// <summary>Block id → state returned by the block.</summary>
    public Dictionary<string, JsonNode> Blocks { get; set; } = new();
    public Dictionary<string, JsonElement> Variables { get; set; } = new();
    public List<string> CompletedEvents { get; set; } = new();
    public List<string> ReadyEvents { get; set; } = new();
    public DateTime SavedAt { get; set; }
}

public sealed class SavedScene
{
    public string Id { get; set; } = "";
    public Dictionary<string, JsonElement> Params { get; set; } = new();
}

public static class SaveService
{
    public static bool Exists => File.Exists(Paths.SaveFile);

    public static void Save(SaveGame save)
    {
        save.SavedAt = DateTime.Now;
        Json.Save(Paths.SaveFile, save);
    }

    public static SaveGame? Load()
    {
        try { return Exists ? Json.Load<SaveGame>(Paths.SaveFile) : null; }
        catch (Exception e)
        {
            Log.Error($"The save file is corrupted and was ignored: {e.Message}");
            return null;
        }
    }

    public static void Delete()
    {
        if (Exists) File.Delete(Paths.SaveFile);
    }
}
