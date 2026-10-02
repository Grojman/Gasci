using Relato.Config;
using Relato.Core;

namespace Relato.UI;

/// <summary>Drawing helpers shared by the blocks. Characters go through the font's character map.</summary>
public static class Frame
{
    public static Theme Theme => Theme.Current;

    public static void Box(ICellSurface surface, Rectangle area, Color color, Color? fill = null)
    {
        if (area.Width <= 0 || area.Height <= 0) return;
        if (fill is { } background) surface.Fill(area, color, background, CharMap.Current.Glyph(' '));
        if (area.Width < 2 || area.Height < 2) return;
        CharMap map = CharMap.Current;
        int h = map.Glyph('─'), v = map.Glyph('│');
        for (int x = area.X + 1; x < area.MaxExtentX; x++)
        {
            surface.SetGlyph(x, area.Y, h, color);
            surface.SetGlyph(x, area.MaxExtentY, h, color);
        }
        for (int y = area.Y + 1; y < area.MaxExtentY; y++)
        {
            surface.SetGlyph(area.X, y, v, color);
            surface.SetGlyph(area.MaxExtentX, y, v, color);
        }
        surface.SetGlyph(area.X, area.Y, map.Glyph('┌'), color);
        surface.SetGlyph(area.MaxExtentX, area.Y, map.Glyph('┐'), color);
        surface.SetGlyph(area.X, area.MaxExtentY, map.Glyph('└'), color);
        surface.SetGlyph(area.MaxExtentX, area.MaxExtentY, map.Glyph('┘'), color);
    }

    /// <summary>Prints text converted to the font's glyphs; characters outside the surface are skipped.</summary>
    public static void Print(ICellSurface surface, int x, int y, string text, Color color)
    {
        if (y < 0 || y >= surface.Height) return;
        CharMap map = CharMap.Current;
        for (int i = 0; i < text.Length; i++)
        {
            if (x + i < 0 || x + i >= surface.Width) continue;
            surface.SetGlyph(x + i, y, map.Glyph(text[i]), color);
        }
    }

    public static void PrintCentered(ICellSurface surface, int y, string text, Color color) =>
        Print(surface, (surface.Width - text.Length) / 2, y, text, color);

    /// <summary>A surface whose cells are normal grid cells (<see cref="Display.CellSize"/>), positioned in pixels.</summary>
    public static ScreenSurface CreateSurface(int width, int height, Color background, int scale = 1)
    {
        var surface = new ScreenSurface(Math.Max(1, width), Math.Max(1, height))
        {
            FontSize = Display.CellSize * scale,
            UsePixelPositioning = true,
        };
        surface.Surface.DefaultBackground = background;
        surface.Surface.DefaultForeground = Theme.Text;
        surface.Surface.Clear();
        return surface;
    }
}
