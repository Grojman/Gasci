using System.Text.Json;
using Gasci.Audio;
using Gasci.Config;
using Gasci.Expressions;
using Gasci.Input;
using Gasci.Layout;
using Gasci.Logic;
using Gasci.Maps;
using Gasci.Rendering;
using Gasci.Scenes;
using Gasci.Variables;

namespace Gasci.Core;

/// <summary>Service locator for the whole game. Content is loaded once at start-up.</summary>
public static class GameServices
{
    public static GameDefinition Game { get; private set; } = new();
    public static Localization Loc { get; private set; } = null!;
    public static TextMetrics Metrics { get; private set; } = null!;
    public static ImageService Images { get; private set; } = null!;
    public static MapService Maps { get; private set; } = null!;
    public static ConversationRepository Conversations { get; private set; } = null!;
    public static EventEngine Events { get; private set; } = null!;
    public static FontCheck.Result? Font { get; private set; }

    /// <summary>Created once MonoGame is running (audio needs the device).</summary>
    public static AudioService? Audio { get; set; }

    public static Random Rng { get; } = new();

    /// <summary>Loads every asset. Throws <see cref="ContentException"/> on content mistakes.</summary>
    public static void LoadContent()
    {
        Game = GameDefinition.Load(Paths.GameFile);
        Theme.Current = Theme.Load(Path.Combine(Paths.Ui, "theme.json"));
        if (Game.Font is { } font)
        {
            Font = FontCheck.Check(font);
            if (Font.Problems.Count > 0) throw new ContentException("Custom font:\n  " + string.Join("\n  ", Font.Problems));
            CharMap.Current = Font.Map!;
        }
        else CharMap.Current = CharMap.Cp437();

        Loc = Localization.Load(Paths.TextsFile, Game.DefaultLanguage);
        if (!Loc.Languages.Contains(Game.DefaultLanguage))
            throw new ContentException($"game.json: defaultLanguage '{Game.DefaultLanguage}' is not a column of dialogs.csv");
        Expression.TextLookup = T;
        Expression.Random = Rng;

        DefineSystemValues();
        VariableRegistry.Load(Paths.Variables, Loc.Languages);
        Expression.ClearCache();
        Metrics = TextMetrics.Load(Loc);
        InputMap.Load(Path.Combine(Paths.Input, "actions.json"), Game.Input.MaxKeys);

        Images = new ImageService(Paths.Images);
        Maps = new MapService(Paths.Maps);
        Conversations = ConversationRepository.Load(Path.Combine(Paths.Data, "conversations.json"));
        Events = new EventEngine(Json.Load<List<EventDefinition>>(Path.Combine(Paths.Data, "events.json")));
        PersistenceScheduler.DelaySeconds = Game.Persistence.DelaySeconds;
    }

    private static void DefineSystemValues()
    {
        VariableRegistry.DefineSystem("save_exists", VarType.Bool, () => Value.Of(SaveService.Exists));
        VariableRegistry.DefineSystem("can_save", VarType.Bool, () => Value.Of(SceneStack.Instance?.CanSave ?? false));
        VariableRegistry.DefineSystem("in_game", VarType.Bool, () => Value.Of(SceneStack.Instance?.InGame ?? false));
        VariableRegistry.DefineSystem("display_mode", VarType.String, () => Value.Of(Display.Mode));
        VariableRegistry.DefineSystem("language", VarType.String, () => Value.Of(Loc.Language));
    }

    /// <summary>Loads the player's files: settings values (settings.json) and key bindings.</summary>
    public static void LoadUserFiles()
    {
        PersistenceScheduler.Register(VariableRegistry.SettingsPersistenceKey,
            () => Json.Save(Paths.SettingsFile, VariableRegistry.Snapshot(VarScope.Settings)));
        Dictionary<string, JsonElement>? values = null;
        if (File.Exists(Paths.SettingsFile))
        {
            try { values = Json.Load<Dictionary<string, JsonElement>>(Paths.SettingsFile); }
            catch (Exception e) { Log.Warn($"settings.json could not be read, using the defaults: {e.Message}"); }
        }
        VariableRegistry.LoadValues(VarScope.Settings, values, "settings.json");
        InputMap.LoadOverrides(Paths.KeyBindingsFile);
    }

    /// <summary>Connects the settings variables to the engine (volumes, language, display mode) and applies them.</summary>
    public static void InstallBindings()
    {
        static float Volume(VariableDefinition def, Value v) => def.Max is { } max && max > 0 ? v.Int / (float)max : v.Int / 10f;
        Bindings.Install(Bindings.MusicVolume, (def, v) => { if (Audio is not null) Audio.MusicVolume = Volume(def, v); });
        Bindings.Install(Bindings.SfxVolume, (def, v) => { if (Audio is not null) Audio.SfxVolume = Volume(def, v); });
        Bindings.Install(Bindings.Language, (_, v) =>
        {
            Loc.Language = v.Str;
            foreach (Scene s in SceneStack.Instance?.Scenes.Values ?? []) s.InvalidateLayout();
        });
        Bindings.Install(Bindings.DisplayMode, (_, v) => Display.Apply(v.Str));
        VariableRegistry.ApplyBindings();
    }

    /// <summary>Shortcut for localised text.</summary>
    public static string T(string key) => Loc.Get(key);
}
