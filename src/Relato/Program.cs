using Relato.Audio;
using Relato.Core;
using Relato.Scenes;
using Relato.Variables;
using SadConsole.Configuration;

// `dotnet run -- --validate` checks every asset without opening a window (and regenerates the text metrics).
if (args.Contains("--validate"))
    return ContentValidator.Run();

Log.FilePath = Paths.LogFile;
try
{
    GameServices.LoadContent();
    GameServices.LoadUserFiles();
    var game = GameServices.Game;
    Display.Configure(game.Grid, GameServices.Font?.GlyphSize ?? new Point(8, 16));

    Settings.WindowTitle = game.Title;
    // The window is only resized by the display mode setting; between changes the grid is scaled to fit.
    Settings.ResizeMode = Settings.WindowResizeOptions.Fit;
    Settings.AllowWindowResize = false;
    // Keyboard only: the engine is meant for terminal-like games.
    SadConsole.Settings.Input.DoMouse = false;

    Point pixels = Display.InitialPixels;
    Builder startup = new Builder()
        .SetWindowSizeInPixels(pixels.X, pixels.Y)
        .ConfigureFonts((fonts, _) =>
        {
            if (GameServices.Font is { } font) fonts.UseCustomFont(font.FontPath);
            else fonts.UseBuiltinFont();
            fonts.SetDefaultFontSize(Display.ToFontSize(Display.FontScale));
        })
        .SetStartingScreen(_ =>
        {
            GameServices.Audio = new AudioService();
            var stack = new SceneStack(Paths.Scenes);
            GameServices.Events.Host = stack;
            GameServices.InstallBindings();
            stack.Start(game.StartScene);
            return stack;
        })
        .IsStartingScreenFocused(true)
        .OnStart((_, _) =>
        {
            Game.Instance.MonoGameInstance.IsMouseVisible = false;
            Display.Start();
        });

    Game.Create(startup);
    Game.Instance.Run();
    PersistenceScheduler.FlushAll();
    GameServices.Audio?.Dispose();
    Game.Instance.Dispose();
    return 0;
}
catch (Exception e)
{
    Log.Error(e is ContentException ? $"Content error: {e.Message}" : $"Fatal error: {e}");
    try { PersistenceScheduler.FlushAll(); } catch { /* the original error matters more */ }
    return 1;
}
