using Gasci.Actions;
using Gasci.Config;
using Gasci.Core;
using Gasci.Expressions;
using Gasci.UI;
using Gasci.Variables;

namespace Gasci.Blocks;

/// <summary>A read-only gauge bound to an int variable (health, progress): ████░░░░.</summary>
public sealed class BarBlock : Block
{
    public string Var { get; set; } = "";
    /// <summary>Maximum: a number or an int expression ("game.max_health"). Defaults to the variable's max.</summary>
    public string? Max { get; set; }
    public string? Min { get; set; }
    public string Full { get; set; } = "█";
    public string Empty { get; set; } = "░";
    public string? EmptyStyle { get; set; }
    /// <summary>Optional text key printed before the bar.</summary>
    public string? Label { get; set; }

    private Expression? _max, _min;
    private VariableDefinition _var = null!;
    private string _rendered = "";

    protected override void OnAttach()
    {
        _var = VariableRegistry.Definition(Var);
        if (_var.Type != VarType.Int) throw new ContentException($"{Where}: a bar needs an int variable, {Var} is {Value.TypeName(_var.Type)}");
        _max = CompileInt(Max);
        _min = CompileInt(Min);
        if (_max is null && _var.Max is null) throw new ContentException($"{Where}: give the bar a 'max' or give {Var} a max in its template");
    }

    private Expression? CompileInt(string? source)
    {
        if (source is null) return null;
        Expression e;
        try { e = Expression.Compile(source); }
        catch (ExpressionException ex) { throw new ContentException($"{Where}: {ex.Message}"); }
        if (e.Type != VarType.Int || e.UsesRandom) throw new ContentException($"{Where}: '{source}' must be an int expression without rand()");
        return e;
    }

    private int LabelWidth => Label is null ? 0 : GameServices.Metrics.Get(Label).Natural + 1;

    protected override Point MeasureContent(Point available) => new(Math.Min(available.X, LabelWidth + 10), 1);

    protected override void OnArrange()
    {
        EnsureSurface(BackgroundColor);
        _rendered = "";
    }

    public override void Update(TimeSpan delta)
    {
        if (Surface is null || !IsShown) return;
        int min = _min?.EvalInt() ?? _var.Min ?? 0;
        int max = _max?.EvalInt() ?? _var.Max!.Value;
        int value = VariableRegistry.Read(_var).Int;
        int width = Math.Max(0, Surface.Surface.Width - LabelWidth);
        int filled = max <= min ? 0 : (int)Math.Round(Math.Clamp((value - min) / (double)(max - min), 0, 1) * width);
        string key = $"{value}/{min}/{max}/{width}/{GameServices.Loc.Language}";
        if (key == _rendered) return;
        _rendered = key;

        ICellSurface s = Surface.Surface;
        s.Clear();
        Theme theme = Theme.Current;
        if (Label is not null) Frame.Print(s, 0, 0, GameServices.T(Label), theme.Text);
        Frame.Print(s, LabelWidth, 0, string.Concat(Enumerable.Repeat(Full, filled)), ContentColor("highlight"));
        Frame.Print(s, LabelWidth + filled, 0, string.Concat(Enumerable.Repeat(Empty, width - filled)), theme.Color(EmptyStyle, "dim"));
    }
}

/// <summary>
/// Procedural effects over the block's area:
///   dissolve: the area fills with random bits until covered, then runs <see cref="OnDone"/> (the exit effect).
///   glitch:   random shade characters flicker; <see cref="Intensity"/> (an int expression) sets how many (0 = none).
/// </summary>
public sealed class EffectBlock : Block
{
    public string Effect { get; set; } = "dissolve";
    public double Speed { get; set; } = 2600;
    public string? Intensity { get; set; }
    public List<ActionDefinition>? OnDone { get; set; }

    private Expression? _intensity;
    private List<Point> _order = new();
    private int _index;
    private double _budget, _afterCovered;
    private bool _done;

    protected override void OnAttach()
    {
        if (Effect is not ("dissolve" or "glitch")) throw new ContentException($"{Where}: unknown effect '{Effect}' (dissolve, glitch)");
        if (Intensity is not null)
        {
            try { _intensity = Expression.Compile(Intensity); }
            catch (ExpressionException e) { throw new ContentException($"{Where}: {e.Message}"); }
            if (_intensity.Type != VarType.Int) throw new ContentException($"{Where}: intensity must be an int expression");
        }
    }

    protected override Point MeasureContent(Point available) => available;

    public override void OnOpen(bool restoring)
    {
        _index = 0;
        _budget = _afterCovered = 0;
        _done = false;
        if (Effect == "dissolve") GameServices.Audio?.StopMusic();
    }

    protected override void OnArrange()
    {
        EnsureSurface(Color.Transparent).Surface.Clear();
        _order = new List<Point>(Bounds.Width * Bounds.Height);
        for (int y = 0; y < Bounds.Height; y++)
            for (int x = 0; x < Bounds.Width; x++) _order.Add(new Point(x, y));
        _order = _order.OrderBy(_ => GameServices.Rng.Next()).ToList();
        _index = 0;
    }

    public override void Update(TimeSpan delta)
    {
        if (Surface is null || !IsShown) return;
        ICellSurface s = Surface.Surface;
        if (Effect == "glitch") { Glitch(s); return; }
        if (_done) return;

        CharMap map = CharMap.Current;
        if (_index < _order.Count)
        {
            _budget += delta.TotalSeconds * Speed;
            for (; _budget >= 1 && _index < _order.Count; _budget--, _index++)
            {
                Point p = _order[_index];
                s.SetGlyph(p.X, p.Y, map.Glyph(GameServices.Rng.Next(2) == 0 ? '0' : '1'), new Color(35, 35, 35), Color.Black);
                if (_index % 40 == 0) GameServices.Audio?.PlayCursor();
            }
            return;
        }

        // Fully covered: the bits fade to pure black, then the effect is done.
        _afterCovered += delta.TotalSeconds;
        if (_afterCovered > 0.35) s.Fill(Color.Black, Color.Black, map.Glyph(' '));
        if (_afterCovered > 0.8)
        {
            _done = true;
            ActionRunner.Run(OnDone, new ActionContext(Scene, this, Where));
        }
    }

    private void Glitch(ICellSurface s)
    {
        s.Clear();
        int intensity = _intensity?.EvalInt() ?? 1;
        if (intensity <= 0 || GameServices.Rng.NextDouble() > 0.12 * intensity) return;
        CharMap map = CharMap.Current;
        string shades = "░▒▓";
        for (int i = 0; i < intensity * 3; i++)
        {
            int x = GameServices.Rng.Next(s.Width), y = GameServices.Rng.Next(s.Height);
            s.SetGlyph(x, y, map.Glyph(shades[GameServices.Rng.Next(3)]), new Color(90 + GameServices.Rng.Next(60), 10, 20));
        }
    }
}

/// <summary>
/// Invisible: runs <see cref="Actions"/> <see cref="Seconds"/> after the scene opens, or — with
/// <see cref="When"/> — after its condition has been true for that long (and again each time it
/// becomes true). <see cref="Repeat"/> runs it every <see cref="Seconds"/>.
/// </summary>
public sealed class TimerBlock : Block
{
    public double Seconds { get; set; }
    public string? When { get; set; }
    public bool Repeat { get; set; }
    public List<ActionDefinition> Actions { get; set; } = new();

    private Expression? _when;
    private double _elapsed;
    private bool _fired;

    protected override void OnAttach()
    {
        _when = Compile(When);
        Visible = false; // takes no space; it is updated anyway
    }

    protected override Point MeasureContent(Point available) => Point.Zero;

    public override void OnOpen(bool restoring)
    {
        _elapsed = 0;
        _fired = false;
    }

    public override void Update(TimeSpan delta)
    {
        if (_when is not null && !_when.EvalBool())
        {
            _elapsed = 0;
            _fired = false;
            return;
        }
        if (_fired && !Repeat) return;
        _elapsed += delta.TotalSeconds;
        if (_elapsed < Seconds) return;
        _elapsed = 0;
        _fired = true;
        ActionRunner.Run(Actions, new ActionContext(Scene, this, Where));
    }
}
