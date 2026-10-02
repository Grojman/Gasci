using System.Text.Json.Nodes;
using Relato.Config;
using Relato.Core;
using Relato.Input;
using Relato.Maps;
using Relato.Scenes;
using Relato.UI;
using Relato.Variables;

namespace Relato.Blocks;

/// <summary>
/// A map viewport. The map is split into sections (game.json → map.sectionWidth/Height); the block
/// shows one section with a frame whose arrows tell in which directions there is more map. Walking
/// off a section loads the next one. If the block is smaller than a section, the map is drawn with
/// smaller characters so the section still fits.
///
/// The map has no message box of its own: interaction texts go to the textbox block named by the
/// map info ("textbox"), or by the tile / NPC. With "player": true the block owns the player (moves
/// with the map.* actions, is saved, receives teleports); with "raiseEvents": true it fires the
/// enterMap / step / halfway triggers.
/// </summary>
public sealed class MapBlock : Block
{
    private const double StepSeconds = 0.1;
    private const double FirstRepeat = 0.18;
    private static readonly Color PlayerColor = new(240, 240, 240);

    /// <summary>Map id or scene parameter ("$map").</summary>
    public string? Map { get; set; }
    /// <summary>Start position (global map cells) or scene parameters; empty = the map's spawn.</summary>
    public string? StartX { get; set; }
    public string? StartY { get; set; }
    public bool Player { get; set; }
    /// <summary>Text key printed on the bottom frame.</summary>
    public string? Hint { get; set; }
    public bool ShowTitle { get; set; } = true;

    private ScreenSurface? _view;
    private ScreenSurface? _sprite;
    private MapData? _map;
    private Point _player, _stepFrom, _facing = new(0, 1);
    private double _stepTime = StepSeconds, _time, _repeatTimer;
    private int _interactedFrame = -1, _frame;

    [System.Text.Json.Serialization.JsonIgnore] public string MapId => _map?.Id ?? "";
    [System.Text.Json.Serialization.JsonIgnore] public Point Position => _player;
    [System.Text.Json.Serialization.JsonIgnore] public Point Facing => _facing;
    private static Point SectionSize => new(GameServices.Game.Map.SectionWidth, GameServices.Game.Map.SectionHeight);
    [System.Text.Json.Serialization.JsonIgnore] public Point Section => SectionOf(_player);
    private bool IsStepping => _stepTime < StepSeconds;
    [System.Text.Json.Serialization.JsonIgnore] public override bool IsIdle => !IsStepping;
    [System.Text.Json.Serialization.JsonIgnore] public override bool IsOpaque => true;

    private static Point SectionOf(Point p) => new(p.X / SectionSize.X, p.Y / SectionSize.Y);

    // ---- Loading -----------------------------------------------------------------------------

    public override void OnOpen(bool restoring)
    {
        if (restoring) return; // RestoreState places the player
        string id = Resolve(Map) ?? throw new ContentException($"{Where}: missing 'map'");
        MapInfo info = GameServices.Maps.Info(id);
        int[] spawn = info.Spawn ?? [1, 1];
        int x = int.TryParse(Resolve(StartX), out int sx) ? sx : spawn[0];
        int y = int.TryParse(Resolve(StartY), out int sy) ? sy : spawn[1];
        Load(id, new Point(x, y));
    }

    /// <summary>Loads a map placing the player at a global map position.</summary>
    public void Load(string mapId, Point position, bool fireEnterEvents = true)
    {
        _map = GameServices.Maps.Load(mapId);
        _player = new Point(Math.Clamp(position.X, 0, _map.Width - 1), Math.Clamp(position.Y, 0, _map.Height - 1));
        _stepFrom = _player;
        _stepTime = StepSeconds;
        GameServices.Audio?.PlayMusic(_map.Info.Music);
        if (fireEnterEvents && RaiseEvents) GameServices.Events.NotifyEnterMap(mapId);
    }

    public override JsonNode? SaveState() => Player && _map is not null
        ? new JsonObject { ["map"] = _map.Id, ["x"] = _player.X, ["y"] = _player.Y, ["fx"] = _facing.X, ["fy"] = _facing.Y }
        : null;

    public override void RestoreState(JsonNode state)
    {
        Load(state["map"]!.GetValue<string>(), new Point(state["x"]!.GetValue<int>(), state["y"]!.GetValue<int>()), fireEnterEvents: false);
        _facing = new Point(state["fx"]?.GetValue<int>() ?? 0, state["fy"]?.GetValue<int>() ?? 1);
    }

    /// <summary>If a variable change selected another drawing for this map (e.g. distorted), swap it keeping the player.</summary>
    private void RefreshVariant()
    {
        if (_map is not null && GameServices.Maps.ResolveFile(_map.Id) != _map.File)
            _map = GameServices.Maps.Load(_map.Id);
    }

    // ---- Layout ------------------------------------------------------------------------------

    protected override Point MeasureContent(Point available) => SectionSize + new Point(2, 2);

    protected override void OnArrange()
    {
        Point viewCells = SectionSize + new Point(2, 2);
        Point cell = Display.CellSize;
        var boundsPx = new Point(Bounds.Width * cell.X, Bounds.Height * cell.Y);
        double f = Math.Min(boundsPx.X / (double)(viewCells.X * cell.X), boundsPx.Y / (double)(viewCells.Y * cell.Y));
        Point charSize = f >= 1 ? cell : new Point(Math.Max(1, (int)(cell.X * f)), Math.Max(1, (int)(cell.Y * f)));

        if (_view is null || _view.FontSize != charSize || _view.Surface.Width != viewCells.X || _view.Surface.Height != viewCells.Y)
        {
            _view = new ScreenSurface(viewCells.X, viewCells.Y) { FontSize = charSize, UsePixelPositioning = true };
            _view.Surface.DefaultBackground = Color.Black;
            _sprite = new ScreenSurface(1, 1) { FontSize = charSize, UsePixelPositioning = true };
            _sprite.Surface.DefaultBackground = Color.Transparent;
            _sprite.Surface.Clear();
            _sprite.Surface.SetGlyph(0, 0, CharMap.Current.Glyph('@'), PlayerColor);
            _view.Children.Add(_sprite);
        }
        _view.Position = Bounds.Position * cell + (boundsPx - viewCells * charSize) / 2;
        Surface = _view;
    }

    // ---- Input -------------------------------------------------------------------------------

    public override bool HandleAction(string action)
    {
        if (_map is null) return false;
        if (action is "map.interact" or InputMap.Confirm)
        {
            if (_interactedFrame != _frame && !IsStepping)
            {
                _interactedFrame = _frame;
                Interact(_player + _facing);
            }
            return true;
        }
        return action.StartsWith("map.");
    }

    private void PollMovement()
    {
        if (!Player || _map is null || IsStepping || !Scene.IsTop || !Scene.Focus.Has(this)) return;
        Point held = Direction(InputMap.IsHeld);
        if (held == Point.Zero) return;
        Point pressed = Direction(InputMap.WasPressed);
        if (pressed != Point.Zero)
        {
            _repeatTimer = FirstRepeat;
            TryMove(pressed, chained: false);
        }
        else if (_repeatTimer <= 0)
            TryMove(held, chained: true);
    }

    private static Point Direction(Func<string, bool> active)
    {
        if (active("map.up")) return new Point(0, -1);
        if (active("map.down")) return new Point(0, 1);
        if (active("map.left")) return new Point(-1, 0);
        if (active("map.right")) return new Point(1, 0);
        return Point.Zero;
    }

    /// <param name="chained">The key was held: the new step continues the previous glide without losing time.</param>
    private void TryMove(Point direction, bool chained)
    {
        MapData map = _map!;
        _facing = direction;
        Point target = _player + direction;
        if (!map.InBounds(target.X, target.Y)) return;

        if (NpcAt(target) is not null || map.IsSolid(target.X, target.Y))
        {
            // Bumping into something interacts with it, but only on a fresh press: a held key would reopen
            // a conversation as soon as it closes.
            if (!chained) Interact(target);
            return;
        }

        Point section = SectionSize;
        int previousLocalX = _player.X - Section.X * section.X;
        Point previousSection = Section;
        _stepFrom = _player;
        _stepTime = chained && _stepTime < StepSeconds + 0.035 ? _stepTime - StepSeconds : 0; // keep the leftover of the last frame
        _player = target;
        int localX = _player.X - Section.X * section.X;
        if (Section != previousSection)
        {
            previousLocalX = localX; // the jump between sections is not "crossing half"
            _stepFrom = _player;     // nor is it animated: the whole view changes
        }
        GameServices.Audio?.PlaySfx("step", 0.25f, 0.3f);

        if (map.ExitAt(target.X, target.Y) is { } exit)
        {
            if (exit.Sfx is not null) GameServices.Audio?.PlaySfx(exit.Sfx);
            Load(exit.Map, new Point(exit.ToX, exit.ToY));
            return;
        }

        if (RaiseEvents) GameServices.Events.NotifyStep(map.Id, _player, Section.X, previousLocalX, localX, section.X);
    }

    private void Interact(Point target)
    {
        MapData map = _map!;
        if (NpcAt(target) is { } npc)
        {
            if (npc.Dialog is not null)
                SceneStack.Instance.Open(GameServices.Game.ConversationScene, new() { ["conversation"] = Value.Of(npc.Dialog) });
            else if (npc.Interact is not null) Message(npc.Interact, npc.Textbox, $"NPC '{npc.Id}'");
            return;
        }
        if (map.TileAt(target.X, target.Y) is { Interact: { } key } tile) Message(key, tile.Textbox, $"tile '{tile.Char}'");
    }

    private void Message(string key, string? textbox, string what)
    {
        string target = textbox ?? _map!.Info.Textbox
            ?? throw new ContentException($"Map '{_map!.Id}': {what} has an interaction text but neither it nor the map names a 'textbox' to show it in");
        SceneStack.Instance.ShowMessage(target, [key], null, $"map '{_map!.Id}'");
    }

    private Npc? NpcAt(Point p) => _map!.VisibleNpcs().FirstOrDefault(n => n.Position == p);

    public void Teleport(string mapId, int x, int y) => Load(mapId, new Point(x, y));

    // ---- Update / drawing --------------------------------------------------------------------

    public override void Update(TimeSpan delta)
    {
        _frame++;
        _time += delta.TotalSeconds;
        _repeatTimer -= delta.TotalSeconds;
        _stepTime += delta.TotalSeconds;
        if (_map is null) return;
        PollMovement();
        RefreshVariant();
        if (IsShown && !IsCulled) Draw();
    }

    private void Draw()
    {
        if (_view is null || _map is null) return;
        MapData map = _map;
        ICellSurface s = _view.Surface;
        Theme theme = Theme.Current;
        s.Clear();
        Frame.Box(s, s.Area, theme.Border);

        Point section = SectionSize;
        var origin = new Point(Section.X * section.X, Section.Y * section.Y);
        int visibleW = Math.Min(section.X, map.Width - origin.X);
        int visibleH = Math.Min(section.Y, map.Height - origin.Y);
        // Maps smaller than a section are centred; bigger ones are split into sections.
        int offsetX = 1 + (map.Width <= section.X ? (section.X - map.Width) / 2 : 0);
        int offsetY = 1 + (map.Height <= section.Y ? (section.Y - map.Height) / 2 : 0);

        for (int y = 0; y < visibleH; y++)
            for (int x = 0; x < visibleW; x++)
            {
                var (glyph, fg, bg) = map.Appearance(origin.X + x, origin.Y + y, _time);
                s.SetGlyph(offsetX + x, offsetY + y, glyph, fg, bg);
            }

        foreach (Npc npc in map.VisibleNpcs())
        {
            Point p = npc.Position - origin;
            if (p.X >= 0 && p.Y >= 0 && p.X < visibleW && p.Y < visibleH)
                s.SetGlyph(offsetX + p.X, offsetY + p.Y, npc.GlyphAt(_time), npc.Foreground);
        }

        if (Player) DrawPlayer(s, origin, offsetX, offsetY);
        else if (_sprite is not null) _sprite.IsVisible = false;
        DrawSectionArrows(s, origin, theme);

        if (ShowTitle && map.Info.TitleKey is { } key) Frame.Print(s, 3, 0, $" {GameServices.T(key)} ", theme.Text);
        if (Hint is not null)
        {
            string hint = $" {GameServices.T(Hint)} ";
            Frame.Print(s, s.Width - hint.Length - 3, s.Height - 1, hint, theme.Dim);
        }
    }

    /// <summary>
    /// The player is a separate 1x1 surface positioned in pixels, interpolated between the cell it
    /// left and the one it is entering. The map glyphs under it are hidden (keeping their background).
    /// </summary>
    private void DrawPlayer(ICellSurface s, Point origin, int offsetX, int offsetY)
    {
        Point from = _stepFrom - origin, to = _player - origin;
        int blank = CharMap.Current.Glyph(' ');
        s.SetGlyph(offsetX + from.X, offsetY + from.Y, blank);
        s.SetGlyph(offsetX + to.X, offsetY + to.Y, blank);

        double t = Math.Clamp(_stepTime / StepSeconds, 0, 1);
        double x = offsetX + from.X + (to.X - from.X) * t;
        double y = offsetY + from.Y + (to.Y - from.Y) * t;
        _sprite!.IsVisible = true;
        _sprite.Position = new Point((int)Math.Round(x * _view!.FontSize.X), (int)Math.Round(y * _view.FontSize.Y));
    }

    private void DrawSectionArrows(ICellSurface s, Point origin, Theme theme)
    {
        bool on = _time % 1.0 < 0.65;
        Color color = on ? theme.Highlight : new Color(90, 20, 20);
        CharMap m = CharMap.Current;
        Point section = SectionSize;
        if (origin.X > 0) s.SetGlyph(0, s.Height / 2, m.Glyph('◄'), color);
        if (origin.X + section.X < _map!.Width) s.SetGlyph(s.Width - 1, s.Height / 2, m.Glyph('►'), color);
        if (origin.Y > 0) s.SetGlyph(s.Width / 2, 0, m.Glyph('▲'), color);
        if (origin.Y + section.Y < _map.Height) s.SetGlyph(s.Width / 2, s.Height - 1, m.Glyph('▼'), color);
    }
}
