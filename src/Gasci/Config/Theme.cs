using Gasci.Core;

namespace Gasci.Config;

/// <summary>assets/data/ui/theme.json: named colours and default UI sounds.</summary>
public sealed class Theme
{
    public Dictionary<string, string> Colors { get; set; } = new();
    public ThemeSounds Sounds { get; set; } = new();

    private readonly Dictionary<string, Color> _parsed = new(StringComparer.OrdinalIgnoreCase);

    public static Theme Current { get; set; } = Defaults();

    public static Theme Load(string path)
    {
        Theme theme = File.Exists(path) ? Json.Load<Theme>(path) : new Theme();
        foreach (var (name, value) in Defaults().Colors) theme.Colors.TryAdd(name, value);
        foreach (var (name, value) in theme.Colors)
        {
            if (!ColorParser.TryParse(value, out Color color))
                throw new ContentException($"theme.json: colour '{name}' has an invalid value '{value}'");
            theme._parsed[name] = color;
        }
        return theme;
    }

    private static Theme Defaults()
    {
        var theme = new Theme
        {
            Colors = new()
            {
                ["text"] = "#c8c8c8", ["dim"] = "#6e6e6e", ["highlight"] = "#e63c3c", ["border"] = "#5a5a64",
                ["disabled"] = "#3c3c3c", ["ok"] = "#78b478", ["background"] = "#000000", ["hint"] = "#505050",
            },
        };
        foreach (var (name, value) in theme.Colors) theme._parsed[name] = ColorParser.Parse(value, SadRogue.Primitives.Color.White);
        return theme;
    }

    public bool Has(string name) => _parsed.ContainsKey(name);

    /// <summary>A theme colour by name, or a literal colour ("DarkRed", "#ff0000").</summary>
    public Color Color(string? name, string fallback = "text")
    {
        if (name is not null && _parsed.TryGetValue(name, out Color c)) return c;
        if (name is not null && ColorParser.TryParse(name, out c)) return c;
        return _parsed.TryGetValue(fallback, out c) ? c : SadRogue.Primitives.Color.White;
    }

    public Color Text => Color("text");
    public Color Dim => Color("dim");
    public Color Highlight => Color("highlight");
    public Color Border => Color("border");
    public Color Disabled => Color("disabled");
}

public sealed class ThemeSounds
{
    public string? Move { get; set; } = "select";
    public string? Confirm { get; set; } = "confirm";
    public string? Locked { get; set; } = "locked";
    public string? Change { get; set; } = "select";
}
