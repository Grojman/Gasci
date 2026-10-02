using System.Text.Json;
using Gasci.Core;

namespace Gasci.Config;

/// <summary>
/// assets/data/game.json: everything about the game that is not content. Values here replace the
/// constants the engine used to have, so another game can change them without touching C#.
/// </summary>
public sealed class GameDefinition
{
    public string Title { get; set; } = "Gasci";
    public GridConfig Grid { get; set; } = new();
    /// <summary>Custom bitmap font; null uses SadConsole's built-in IBM 8x16 font (CP437 layout).</summary>
    public FontConfigDefinition? Font { get; set; }
    public MapConfig Map { get; set; } = new();
    public ImageConfig Images { get; set; } = new();
    public string DefaultLanguage { get; set; } = "en";
    /// <summary>Scene shown when the game starts.</summary>
    public string StartScene { get; set; } = "title";
    /// <summary>Scene (and its parameters) opened by the newGame action.</summary>
    public SceneRequest NewGame { get; set; } = new();
    /// <summary>Scene used by the "dialog" action and by NPC conversations (param: conversation).</summary>
    public string ConversationScene { get; set; } = "dialog";
    /// <summary>Scene used by the "black" action (params: key, image, seconds, big, drawMode, cursorMode).</summary>
    public string CardScene { get; set; } = "black";
    /// <summary>Scene opened by the quit action before closing (an exit effect); null closes at once.</summary>
    public string? QuitScene { get; set; }
    public PersistenceConfig Persistence { get; set; } = new();
    public InputConfig Input { get; set; } = new();

    public static GameDefinition Load(string path) => Json.Load<GameDefinition>(path);
}

public sealed class GridConfig
{
    /// <summary>Smallest grid, in cells, the game is designed for. Windowed mode uses exactly this size.</summary>
    public int MinWidth { get; set; } = 100;
    public int MinHeight { get; set; } = 36;
    /// <summary>Font scales to try, biggest first (1, 2, 3, 4; 0.5 is not supported).</summary>
    public List<int> FontScales { get; set; } = [2, 1];
}

public sealed class FontConfigDefinition
{
    /// <summary>SadConsole .font file, relative to assets/fonts.</summary>
    public string File { get; set; } = "";
    /// <summary>
    /// Character map, relative to assets/fonts: a UTF-8 text file with one line per row of the tile
    /// sheet; the n-th character of the file (new lines excluded) is drawn with glyph n.
    /// </summary>
    public string Charmap { get; set; } = "";
}

public sealed class MapConfig
{
    /// <summary>Size of a map section (the part of a map shown at once). Independent of the screen.</summary>
    public int SectionWidth { get; set; } = 98;
    public int SectionHeight { get; set; } = 34;
}

public sealed class ImageConfig
{
    /// <summary>
    /// Smallest image character in pixels at font scale 1 (multiplied by the scale in use). Its shape
    /// is the shape of every image character in contain/cover modes. [1.5, 3] = 3x6 pixels at scale 2.
    /// </summary>
    public double[] MinCharSize { get; set; } = [1.5, 3];
    /// <summary>Image characters per normal cell side for the "none" fit mode.</summary>
    public int Detail { get; set; } = 2;
}

public sealed class PersistenceConfig
{
    /// <summary>Seconds without changes before settings and key bindings are written to disk.</summary>
    public double DelaySeconds { get; set; } = 1.0;
}

public sealed class InputConfig
{
    /// <summary>Keys per action (primary, secondary...).</summary>
    public int MaxKeys { get; set; } = 2;
}

public sealed class SceneRequest
{
    public string Scene { get; set; } = "game";
    public Dictionary<string, JsonElement> Params { get; set; } = new();
}
