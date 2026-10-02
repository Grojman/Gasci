using Relato.Core;
using Relato.Expressions;

namespace Relato.Maps;

/// <summary>A loaded map: the character grid plus its info.</summary>
public sealed class MapData
{
    private readonly char[,] _chars;
    private readonly Dictionary<char, TileInfo> _tiles;

    public string Id { get; }
    /// <summary>Name of the .txt drawing in use (changes with variants).</summary>
    public string File { get; }
    public MapInfo Info { get; }
    public int Width { get; }
    public int Height { get; }

    public MapData(string id, string file, MapInfo info, IReadOnlyList<string> lines)
    {
        Id = id;
        File = file;
        Info = info;
        Height = lines.Count;
        Width = lines.Max(l => l.Length);
        _chars = new char[Width, Height];
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                _chars[x, y] = x < lines[y].Length ? lines[y][x] : ' ';

        _tiles = info.Tiles.Where(t => t.Char.Length > 0).ToDictionary(t => t.Char[0]);
    }

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
    public char CharAt(int x, int y) => InBounds(x, y) ? _chars[x, y] : ' ';
    public TileInfo? TileAt(int x, int y) => _tiles.GetValueOrDefault(CharAt(x, y));

    public bool IsSolid(int x, int y) => !InBounds(x, y) || (TileAt(x, y)?.Solid ?? false);

    public ExitInfo? ExitAt(int x, int y) =>
        Info.Exits.FirstOrDefault(e => e.X == x && e.Y == y && Expression.IsTrue(e.If));

    /// <summary>Glyph and colours to draw a cell at a given animation time.</summary>
    public (int glyph, Color fg, Color bg) Appearance(int x, int y, double time)
    {
        char c = CharAt(x, y);
        TileInfo? tile = TileAt(x, y);
        if (tile is null) return (CharMap.Current.Glyph(c), new Color(140, 140, 140), Color.Black);

        string glyph = tile.Glyph ?? tile.Char;
        if (tile.Anim is { Count: > 0 } frames)
            glyph = frames[(int)(time / Math.Max(0.05, tile.AnimSpeed)) % frames.Count];

        return (CharMap.Current.Glyph(glyph.Length > 0 ? glyph[0] : ' '),
                ColorParser.Parse(tile.Fg, new Color(140, 140, 140)),
                ColorParser.Parse(tile.Bg, Color.Black));
    }

    /// <summary>Characters placed on the map, resolved against the current variables.</summary>
    public IEnumerable<Npc> VisibleNpcs()
    {
        foreach (NpcInfo info in Info.Npcs)
        {
            if (!Expression.IsTrue(info.If)) continue;
            NpcVariant? variant = info.Variants.FirstOrDefault(v => Expression.IsTrue(v.If));
            yield return new Npc(info, variant);
        }
    }
}

/// <summary>A character on the map after applying its variant.</summary>
public sealed class Npc(NpcInfo info, NpcVariant? variant)
{
    public string Id => info.Id;
    public Point Position => new(info.X, info.Y);
    public string Glyph => variant?.Glyph ?? info.Glyph ?? "?";
    public Color Foreground => ColorParser.Parse(variant?.Fg ?? info.Fg, Color.White);
    public string? Dialog => NullIfEmpty(variant?.Dialog ?? info.Dialog);
    public string? Interact => NullIfEmpty(variant?.Interact ?? info.Interact);
    public string? Textbox => variant?.Textbox ?? info.Textbox;
    public List<string>? Anim => variant?.Anim ?? info.Anim;
    public double AnimSpeed => variant?.AnimSpeed ?? info.AnimSpeed ?? 0.5;

    public int GlyphAt(double time)
    {
        string glyph = Anim is { Count: > 0 } frames
            ? frames[(int)(time / Math.Max(0.05, AnimSpeed)) % frames.Count]
            : Glyph;
        return CharMap.Current.Glyph(glyph.Length > 0 ? glyph[0] : '?');
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;
}
