using Microsoft.Xna.Framework.Graphics;
using Gasci.Config;

namespace Gasci.Core;

/// <summary>
/// Window mode and grid size. The window cannot be resized by hand: it only changes when the display
/// mode setting changes (windowed / fullscreen / borderless), and that is the only moment the grid
/// is recomputed and every scene re-laid out.
///  - windowed:   exactly the developer's minimum grid, scaled to 90 % of the screen.
///  - fullscreen / borderless: the minimum grid scaled to fill the screen on its tighter axis; the
///    other axis gets extra cells (percent layouts adapt to them; fixed cell sizes do not).
/// </summary>
public static class Display
{
    public const string Windowed = "windowed", FullScreen = "fullscreen", Borderless = "borderless";
    private const double WindowScreenShare = 0.9;

    private static GridConfig _config = new();
    private static bool _isFullScreen, _started;

    /// <summary>Grid size in cells.</summary>
    public static Point Grid { get; private set; } = new(100, 36);
    /// <summary>Size in pixels of a normal cell (font glyph × font scale).</summary>
    public static Point CellSize { get; private set; } = new(16, 32);
    /// <summary>Glyph size of the font at scale 1.</summary>
    public static Point GlyphSize { get; private set; } = new(8, 16);
    public static int FontScale { get; private set; } = 2;
    public static string Mode { get; private set; } = Windowed;

    /// <summary>Raised after the grid or the cell size changed.</summary>
    public static event Action? GridChanged;

    /// <summary>Called before the window exists: the starting grid, used until <see cref="Apply"/> runs.</summary>
    public static void Configure(GridConfig config, Point glyphSize)
    {
        _config = config;
        GlyphSize = glyphSize;
        FontScale = config.FontScales.Count > 0 ? config.FontScales.Max() : 1; // what windowed mode uses
        CellSize = GlyphSize * FontScale;
        Grid = new Point(config.MinWidth, config.MinHeight);
    }

    /// <summary>Starting window size in pixels (before the mode is applied).</summary>
    public static Point InitialPixels => Grid * CellSize;

    public static IFont.Sizes ToFontSize(int scale) => scale switch
    {
        1 => IFont.Sizes.One,
        2 => IFont.Sizes.Two,
        3 => IFont.Sizes.Three,
        4 => IFont.Sizes.Four,
        _ => throw new ContentException($"game.json: font scale {scale} is not supported (use 1, 2, 3 or 4)"),
    };

    /// <summary>Called once the window exists: applies the mode chosen so far.</summary>
    public static void Start()
    {
        _started = true;
        Apply(Mode);
    }

    public static void Apply(string mode)
    {
        if (!Variables.Bindings.DisplayModes.Contains(mode)) { Log.Warn($"Unknown display mode '{mode}', using windowed"); mode = Windowed; }
        Mode = mode;
        if (!_started) return; // the window does not exist yet: Start applies it

        DisplayMode screen = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        var screenSize = new Point(screen.Width, screen.Height);
        var game = (SadConsole.Game)Game.Instance;
        var window = game.MonoGameInstance.Window;
        List<int> scales = _config.FontScales.OrderByDescending(s => s).ToList();
        var min = new Point(_config.MinWidth, _config.MinHeight);

        // How much the minimum grid at a font scale has to be scaled to fit an area.
        double FitFactor(int scale, double shareOfScreen) => Math.Min(
            screenSize.X * shareOfScreen / (min.X * GlyphSize.X * scale),
            screenSize.Y * shareOfScreen / (min.Y * GlyphSize.Y * scale));

        if (mode == Windowed)
        {
            if (_isFullScreen) { game.ToggleFullScreen(); _isFullScreen = false; }
            window.IsBorderless = false;
            SetCells(scales[0], min);
            Point render = Grid * CellSize;
            SetRender(render);
            double fit = FitFactor(scales[0], WindowScreenShare);
            var windowSize = new Point((int)(render.X * fit), (int)(render.Y * fit));
            game.ResizeWindow(windowSize.X, windowSize.Y, false);
            window.Position = new Microsoft.Xna.Framework.Point((screenSize.X - windowSize.X) / 2, (screenSize.Y - windowSize.Y) / 2);
        }
        else
        {
            // The biggest font that does not need to shrink below half its size; the minimum grid is scaled to
            // fill the screen on its tighter axis and the other axis gets extra cells (no black bars).
            int scale = scales.FirstOrDefault(s => FitFactor(s, 1) >= 0.5, scales[^1]);
            double fit = FitFactor(scale, 1);
            Point cell = GlyphSize * scale;
            SetCells(scale, new Point(
                Math.Max(min.X, (int)Math.Floor(screenSize.X / (cell.X * fit))),
                Math.Max(min.Y, (int)Math.Floor(screenSize.Y / (cell.Y * fit)))));

            if (mode == FullScreen)
            {
                window.IsBorderless = false;
                if (!_isFullScreen) { game.ToggleFullScreen(); _isFullScreen = true; }
            }
            else
            {
                if (_isFullScreen) { game.ToggleFullScreen(); _isFullScreen = false; }
                window.IsBorderless = true;
                window.Position = Microsoft.Xna.Framework.Point.Zero;
            }
            SetRender(Grid * CellSize);
            game.ResizeWindow(screenSize.X, screenSize.Y, false);
        }
        Input.InputMap.IgnoreHeldKeys(); // the key that changed the mode must not auto-repeat into another change
        GridChanged?.Invoke();
    }

    /// <summary>Size of SadConsole's output; ResizeMode.Fit scales it to the window keeping its proportions.</summary>
    private static void SetRender(Point pixels)
    {
        SadConsole.Settings.Rendering.RenderWidth = pixels.X;
        SadConsole.Settings.Rendering.RenderHeight = pixels.Y;
    }

    private static void SetCells(int scale, Point grid)
    {
        FontScale = scale;
        CellSize = GlyphSize * scale;
        Grid = grid;
    }
}
