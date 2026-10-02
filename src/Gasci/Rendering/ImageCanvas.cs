using Gasci.Core;

namespace Gasci.Rendering;

/// <summary>How an image is fitted into its area, as CSS object-fit.</summary>
public enum ImageFit
{
    /// <summary>The biggest characters that show the whole image keeping its proportions (letterboxed).</summary>
    Contain,
    /// <summary>The smallest characters that fill the whole area keeping proportions; what overflows is cut.</summary>
    Cover,
    /// <summary>Characters stretched separately in X and Y to fill the area exactly (the drawing is deformed).</summary>
    Fill,
    /// <summary>Characters at the default detail; what does not fit is cut.</summary>
    None,
    /// <summary>None if the image fits, Contain otherwise.</summary>
    ScaleDown,
}

/// <summary>
/// Surface where ASCII images are drawn. It covers the same pixels as a region of normal cells but
/// uses its own (usually smaller) characters, so an image can have more detail than the text grid.
/// </summary>
public static class ImageCanvas
{
    /// <param name="parent">Surface the canvas is placed on.</param>
    /// <param name="areaPixels">Region of the parent, in pixels, that the canvas covers.</param>
    /// <param name="charSize">Size in pixels of each image character.</param>
    public static ScreenSurface Create(ScreenSurface parent, Rectangle areaPixels, Point charSize)
    {
        charSize = new Point(Math.Max(1, charSize.X), Math.Max(1, charSize.Y));
        int width = Math.Max(1, areaPixels.Width / charSize.X), height = Math.Max(1, areaPixels.Height / charSize.Y);
        // The pixels left over by big characters are split on both sides so the canvas stays centred.
        var canvas = new ScreenSurface(width, height)
        {
            FontSize = charSize,
            UsePixelPositioning = true,
            Position = new Point(areaPixels.X + (areaPixels.Width - width * charSize.X) / 2,
                                 areaPixels.Y + (areaPixels.Height - height * charSize.Y) / 2),
        };
        canvas.Surface.DefaultBackground = Color.Transparent;
        canvas.Surface.Clear();
        parent.Children.Add(canvas);
        return canvas;
    }

    /// <summary>
    /// Character size for an image in an area, following the fit mode. <paramref name="minCharSize"/> is
    /// the smallest readable character and sets the proportions used by contain and cover;
    /// <paramref name="detailCharSize"/> is the size used by none.
    /// </summary>
    public static Point CharSizeFor(ImageFit fit, Point areaPixels, AsciiImage image, Point minCharSize, Point detailCharSize)
    {
        int columns = Math.Max(image.Width, 1), rows = Math.Max(image.Height, 1);
        switch (fit)
        {
            case ImageFit.None:
                return detailCharSize;
            case ImageFit.ScaleDown:
                return columns * detailCharSize.X <= areaPixels.X && rows * detailCharSize.Y <= areaPixels.Y
                    ? detailCharSize
                    : FitCharSize(areaPixels, image, minCharSize);
            case ImageFit.Fill:
                return new Point(Math.Max(1, areaPixels.X / columns), Math.Max(1, areaPixels.Y / rows));
            case ImageFit.Cover:
            {
                double shape = (double)minCharSize.X / minCharSize.Y;
                int y = Math.Max((int)Math.Ceiling((double)areaPixels.Y / rows), (int)Math.Ceiling(areaPixels.X / (columns * shape)));
                y = Math.Max(y, minCharSize.Y);
                return new Point(Math.Max(minCharSize.X, (int)Math.Ceiling(y * shape)), y);
            }
            default:
                return FitCharSize(areaPixels, image, minCharSize);
        }
    }

    /// <summary>
    /// Biggest image character that still shows the whole image inside <paramref name="areaPixels"/>, keeping
    /// the proportions of <paramref name="minCharSize"/>: small images are stretched to fill the area without
    /// being deformed. Pixel sizes are whole numbers, so sizes whose proportion is off by more than
    /// <see cref="MaxShapeError"/> are skipped. Never returns less than <paramref name="minCharSize"/> (the
    /// image is then cut, and a warning is logged by the caller).
    /// </summary>
    public static Point FitCharSize(Point areaPixels, AsciiImage image, Point minCharSize)
    {
        int columns = Math.Max(image.Width, 1), rows = Math.Max(image.Height, 1);
        double shape = (double)minCharSize.X / minCharSize.Y;
        for (int y = areaPixels.Y / rows; y > minCharSize.Y; y--)
        {
            int x = (int)Math.Round(y * shape);
            if (x * columns <= areaPixels.X && Math.Abs(x / (double)y - shape) <= shape * MaxShapeError)
                return new Point(x, y);
        }
        return minCharSize;
    }

    /// <summary>Largest relative difference between the shape of a stretched image character and the requested one.</summary>
    private const double MaxShapeError = 0.05;

    /// <summary>A canvas with smaller characters has more cells, so cursors write faster to take the same time.</summary>
    public static DrawCursor Scaled(DrawCursor cursor, ScreenSurface canvas)
    {
        Point cell = Display.CellSize;
        cursor.CellsPerSecond *= (double)(cell.X * cell.Y) / (canvas.FontSize.X * canvas.FontSize.Y);
        return cursor;
    }
}
