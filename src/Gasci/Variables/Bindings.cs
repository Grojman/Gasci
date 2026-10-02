namespace Gasci.Variables;

/// <summary>
/// Engine side effects a settings variable can be bound to with "bind". The names and types are fixed
/// here (so --validate can check them); the handlers are installed by the game at start-up.
/// </summary>
public static class Bindings
{
    public const string MusicVolume = "audio.music";
    public const string SfxVolume = "audio.sfx";
    public const string Language = "loc.language";
    public const string DisplayMode = "display.mode";
    public const string Autosave = "save.autosave";
    public const string AutosaveMinutes = "save.autosaveMinutes";

    public static readonly IReadOnlyDictionary<string, VarType> Known = new Dictionary<string, VarType>
    {
        [MusicVolume] = VarType.Int,
        [SfxVolume] = VarType.Int,
        [Language] = VarType.String,
        [DisplayMode] = VarType.String,
        [Autosave] = VarType.Bool,
        [AutosaveMinutes] = VarType.Int,
    };

    /// <summary>Allowed values of the display mode binding.</summary>
    public static readonly string[] DisplayModes = ["windowed", "fullscreen", "borderless"];

    private static readonly Dictionary<string, Action<VariableDefinition, Value>> Handlers = new();

    public static void Install(string binding, Action<VariableDefinition, Value> handler) => Handlers[binding] = handler;

    public static void Clear() => Handlers.Clear();

    /// <summary>The variable bound to a binding, if the game declared one.</summary>
    public static VariableDefinition? VariableFor(string binding) =>
        VariableRegistry.Definitions.FirstOrDefault(d => d.Bind == binding);

    internal static void Apply(VariableDefinition definition, Value value)
    {
        if (definition.Bind is { } bind && Handlers.TryGetValue(bind, out var handler))
        {
            try { handler(definition, value); }
            catch (Exception e) { Core.Log.Error(e, $"Binding '{bind}' of {definition.Name}"); }
        }
    }
}
