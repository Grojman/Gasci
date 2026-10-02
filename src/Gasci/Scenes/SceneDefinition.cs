using System.Text.Json;
using System.Text.Json.Serialization;
using Gasci.Actions;
using Gasci.Blocks;
using Gasci.Layout;
using Size = Gasci.Layout.Size;

namespace Gasci.Scenes;

/// <summary>A scene file (assets/data/scenes/&lt;id&gt;.json).</summary>
public sealed class SceneDefinition
{
    /// <summary>Parameters the scene needs, with their type ("int", "bool", "string"). Blocks read them as "$name".</summary>
    public Dictionary<string, string> Params { get; set; } = new();
    public ScenePresentation Presentation { get; set; } = ScenePresentation.Fill;
    /// <summary>Music to play when the scene opens: missing/null keeps the current one, "" stops it.</summary>
    public string? Music { get; set; }
    /// <summary>The scene is part of the game state: it is stored in the save (with its parameters and block states).</summary>
    public bool Saved { get; set; }
    /// <summary>Handlers for input actions no focused block consumed.</summary>
    public Dictionary<string, List<ActionDefinition>>? Inputs { get; set; }
    public List<ActionDefinition>? OnEnter { get; set; }
    public List<ActionDefinition>? OnResume { get; set; }
    public List<ActionDefinition>? OnLeave { get; set; }
    public Block Root { get; set; } = null!;
}

/// <summary>
/// Where the scene's root block goes on the screen and how the scenes below are shown.
/// "fill" = the whole screen, opaque. "modal" = sized to its content, centred, over a dimmed scene below.
/// </summary>
[JsonConverter(typeof(PresentationConverter))]
public sealed record ScenePresentation
{
    public Size Width { get; init; } = Size.Fill;
    public Size Height { get; init; } = Size.Fill;
    public Anchor X { get; init; } = Anchor.Center;
    public Anchor Y { get; init; } = Anchor.Center;
    /// <summary>none: the scene below is drawn as it is; dim: under a translucent layer; opaque: not drawn.</summary>
    public Backdrop Backdrop { get; init; } = Backdrop.Opaque;

    public static readonly ScenePresentation Fill = new();
    public static readonly ScenePresentation Modal = new() { Width = Size.Auto, Height = Size.Auto, Backdrop = Backdrop.Dim };
}

public sealed class PresentationConverter : JsonConverter<ScenePresentation>
{
    private sealed class Raw
    {
        public Size? Width { get; set; }
        public Size? Height { get; set; }
        public Anchor? X { get; set; }
        public Anchor? Y { get; set; }
        public Backdrop? Backdrop { get; set; }
        /// <summary>Start from a preset and override some values.</summary>
        public string? Preset { get; set; }
    }

    public override ScenePresentation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String) return Preset(reader.GetString());
        Raw raw = JsonSerializer.Deserialize<Raw>(ref reader, options) ?? new Raw();
        ScenePresentation p = Preset(raw.Preset ?? "fill");
        return p with
        {
            Width = raw.Width ?? p.Width,
            Height = raw.Height ?? p.Height,
            X = raw.X ?? p.X,
            Y = raw.Y ?? p.Y,
            Backdrop = raw.Backdrop ?? p.Backdrop,
        };
    }

    private static ScenePresentation Preset(string? name) => name switch
    {
        "fill" => ScenePresentation.Fill,
        "modal" => ScenePresentation.Modal,
        _ => throw new JsonException($"unknown presentation '{name}' (fill, modal, or an object)"),
    };

    public override void Write(Utf8JsonWriter writer, ScenePresentation value, JsonSerializerOptions options) =>
        throw new NotSupportedException();
}
