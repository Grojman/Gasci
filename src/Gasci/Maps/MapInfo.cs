namespace Gasci.Maps;

/// <summary>
/// Contents of &lt;map&gt;_info.json. The map drawing lives in &lt;map&gt;.txt; this file explains what
/// the characters of the drawing mean. Characters not listed in <see cref="Tiles"/> are walkable
/// and drawn as they are.
/// </summary>
public sealed class MapInfo
{
    public string? TitleKey { get; set; }
    public string? Music { get; set; }
    public List<TileInfo> Tiles { get; set; } = new();
    public List<ExitInfo> Exits { get; set; } = new();
    public List<NpcInfo> Npcs { get; set; } = new();
    /// <summary>Alternative drawings chosen by condition (the world becoming distorted...). First match wins.</summary>
    public List<MapVariant> Variants { get; set; } = new();
    public int[]? Spawn { get; set; }
    /// <summary>Id of the textbox block where the interaction texts of this map are shown (tiles and NPCs may override it).</summary>
    public string? Textbox { get; set; }
}

public sealed class TileInfo
{
    /// <summary>The character used in the .txt file.</summary>
    public string Char { get; set; } = " ";
    /// <summary>Character drawn on screen (defaults to <see cref="Char"/>).</summary>
    public string? Glyph { get; set; }
    public string? Fg { get; set; }
    public string? Bg { get; set; }
    public bool Solid { get; set; }
    /// <summary>Text key shown in a message box when the player bumps into / interacts with the tile.</summary>
    public string? Interact { get; set; }
    /// <summary>Textbox block for <see cref="Interact"/>; defaults to the map's.</summary>
    public string? Textbox { get; set; }
    /// <summary>Animation frames (glyphs) cycled every <see cref="AnimSpeed"/> seconds.</summary>
    public List<string>? Anim { get; set; }
    public double AnimSpeed { get; set; } = 0.5;
}

public sealed class ExitInfo
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Map { get; set; } = "";
    public int ToX { get; set; }
    public int ToY { get; set; }
    public string? Sfx { get; set; } = "door";
    public string? If { get; set; }
}

/// <summary>Visual + interaction data of a character on the map. Variants override non-null fields.</summary>
public class NpcLook
{
    public string? Glyph { get; set; }
    public string? Fg { get; set; }
    /// <summary>Conversation id started when interacting. An empty string removes the base conversation.</summary>
    public string? Dialog { get; set; }
    /// <summary>Message key shown when interacting and there is no conversation.</summary>
    public string? Interact { get; set; }
    /// <summary>Textbox block for <see cref="Interact"/>; defaults to the map's.</summary>
    public string? Textbox { get; set; }
    public List<string>? Anim { get; set; }
    public double? AnimSpeed { get; set; }
}

public sealed class NpcInfo : NpcLook
{
    public string Id { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    /// <summary>Condition for the character to exist on the map.</summary>
    public string? If { get; set; }
    public List<NpcVariant> Variants { get; set; } = new();
}

public sealed class NpcVariant : NpcLook
{
    public string If { get; set; } = "";
}

public sealed class MapVariant
{
    public string If { get; set; } = "";
    public string File { get; set; } = "";
}
