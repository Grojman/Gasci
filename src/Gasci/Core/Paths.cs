namespace Gasci.Core;

/// <summary>Locations of the game assets and of the per-user files (settings, save, key bindings, log).</summary>
public static class Paths
{
    /// <summary>Assets next to the executable (copied there by the build). Tests and tools may point it elsewhere.</summary>
    public static string Assets { get; set; } = Path.Combine(AppContext.BaseDirectory, "assets");
    public static string Data => Path.Combine(Assets, "data");
    public static string Maps => Path.Combine(Data, "maps");
    public static string Variables => Path.Combine(Data, "variables");
    public static string Scenes => Path.Combine(Data, "scenes");
    public static string Input => Path.Combine(Data, "input");
    public static string Ui => Path.Combine(Data, "ui");
    public static string Generated => Path.Combine(Data, "generated");
    public static string Images => Path.Combine(Assets, "images");
    public static string Audio => Path.Combine(Assets, "audio");
    public static string Fonts => Path.Combine(Assets, "fonts");

    public static string GameFile => Path.Combine(Data, "game.json");
    public static string TextsFile => Path.Combine(Data, "dialogs.csv");
    public static string TextMetricsFile => Path.Combine(Generated, "text_metrics.csv");

    /// <summary>Folder for the player's files. Can be redirected (tests, portable builds).</summary>
    public static string UserData
    {
        get
        {
            string path = UserDataOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), UserFolderName);
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string? UserDataOverride { get; set; }

    private static string? _userFolderName;

    /// <summary>
    /// Name of the player's folder: the game's title (game.json), so every game made with the engine
    /// keeps its own files. Read on first use, before the content is loaded, so the log of a failed load
    /// still goes to the right folder; "Gasci" when game.json cannot be read.
    /// </summary>
    public static string UserFolderName => _userFolderName ??= ReadUserFolderName();

    private static string ReadUserFolderName()
    {
        try { return ToFolderName(Config.GameDefinition.Load(GameFile).Title); }
        catch { return "Gasci"; }
    }

    /// <summary>A game title as a folder name valid on every platform; "Gasci" when nothing is left.</summary>
    public static string ToFolderName(string? title)
    {
        const string invalid = "<>:\"/\\|?*";
        string name = new string((title ?? "").Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c).ToArray())
            .Trim().TrimEnd('.');
        return name.Length > 0 ? name : "Gasci";
    }

    public static string SettingsFile => Path.Combine(UserData, "settings.json");
    public static string SaveFile => Path.Combine(UserData, "save.json");
    public static string KeyBindingsFile => Path.Combine(UserData, "keybindings.json");
    public static string LogFile => Path.Combine(UserData, "log.txt");

    /// <summary>
    /// The assets folder of the source tree (the one the developer edits), found by walking up from
    /// the executable until a folder holds assets/data/game.json outside bin/. Used by --validate to
    /// write generated files where they will be kept. Falls back to <see cref="Assets"/>.
    /// </summary>
    public static string SourceAssets()
    {
        DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "assets");
            bool insideBuild = dir.FullName.Split(Path.DirectorySeparatorChar).Contains("bin");
            if (!insideBuild && File.Exists(Path.Combine(candidate, "data", "game.json"))) return candidate;
            dir = dir.Parent;
        }
        return Assets;
    }
}
