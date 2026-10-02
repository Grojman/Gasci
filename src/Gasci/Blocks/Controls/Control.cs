using System.Text.Json.Serialization;
using Gasci.Actions;
using Gasci.Config;
using Gasci.Core;
using Gasci.UI;
using Gasci.Variables;

namespace Gasci.Blocks.Controls;

/// <summary>
/// Base of the menu controls: a one-line block with a label. The menu sets <see cref="Selected"/>.
/// Controls bound to a variable keep it inside its rules on screen (sliders stop at their limits,
/// text fields stop accepting characters at maxLength) so the player is never silently corrected.
/// </summary>
public abstract class Control : Block
{
    /// <summary>Text key of the label.</summary>
    public string? Label { get; set; }

    [JsonIgnore] public bool Selected { get; internal set; }
    /// <summary>Set by the menu so that every value starts in the same column.</summary>
    [JsonIgnore] internal int LabelColumn { get; set; }
    /// <summary>Whether the control shows "label  value" and takes part in the menu's label column.</summary>
    internal virtual bool HasLabelColumn => false;
    internal virtual int LabelWidth => Label is null ? 0 : GameServices.Metrics.Get(Label).Natural;
    /// <summary>The control is editing/listening and takes every key (text fields, key binds).</summary>
    [JsonIgnore] public virtual bool IsCapturing => false;

    private string _rendered = "";

    protected string LabelText => Label is null ? "" : GameServices.T(Label);

    protected Color TextColor
    {
        get
        {
            Theme theme = Theme.Current;
            return !IsEnabled ? theme.Disabled : Selected ? theme.Highlight : ContentColor();
        }
    }

    protected override void OnArrange()
    {
        EnsureSurface(BackgroundColor);
        _rendered = "";
    }

    /// <summary>The line to draw (already padded/aligned to the control's width).</summary>
    protected abstract string Render(int width);

    public override void Update(TimeSpan delta)
    {
        if (Surface is null || !IsShown) return;
        ICellSurface s = Surface.Surface;
        string line = Render(s.Width);
        Color color = TextColor;
        string key = line + color.PackedValue;
        if (key == _rendered) return;
        _rendered = key;
        s.Clear();
        Frame.Print(s, 0, 0, line, color);
    }

    protected static void Play(string? sfx, float volume = 1f)
    {
        if (sfx is not null) GameServices.Audio?.PlaySfx(sfx, volume);
    }

    protected bool CheckEnabled()
    {
        if (IsEnabled) return true;
        Play(Theme.Current.Sounds.Locked);
        return false;
    }
}

/// <summary>A control that shows "label   value" and is bound to a variable.</summary>
public abstract class ValueControl : Control
{
    /// <summary>Variable the control edits ("settings.music_volume").</summary>
    public string Var { get; set; } = "";
    /// <summary>Actions run after the value changed (a sound, another variable...).</summary>
    public List<ActionDefinition>? OnChange { get; set; }

    [JsonIgnore] protected VariableDefinition Variable { get; private set; } = null!;

    internal override bool HasLabelColumn => true;

    protected override void OnAttach()
    {
        Variable = VariableRegistry.Definition(Var);
        if (Variable.IsReadOnly) throw new ContentException($"{Where}: {Var} is read-only");
        CheckVariable();
    }

    /// <summary>Checks the variable has the type the control needs.</summary>
    protected abstract void CheckVariable();

    protected Value Current => VariableRegistry.Read(Variable);

    /// <summary>Widest value text (so the control keeps its width while the value changes).</summary>
    protected abstract int ValueWidth { get; }
    protected abstract string ValueText { get; }

    protected override Point MeasureContent(Point available) =>
        new(Math.Min(available.X, Math.Max(LabelColumn, LabelWidth) + 2 + ValueWidth), 1);

    protected override string Render(int width)
    {
        string label = LabelText.PadRight(Math.Max(LabelColumn, LabelWidth));
        return (label + "  " + ValueText).PadRight(width);
    }

    protected void SetValue(Value value)
    {
        if (!VariableRegistry.Set(Variable, value)) return;
        Play(Theme.Current.Sounds.Change, 0.8f);
        ActionRunner.Run(OnChange, new ActionContext(Scene, this, Where));
        NotifyChanged();
    }

    protected static string Arrows(string value, int width) => "◄ " + value.PadRight(width) + " ►";
}

/// <summary>Runs its actions with ui.confirm.</summary>
public sealed class ButtonControl : Control
{
    public List<ActionDefinition> Actions { get; set; } = new();

    protected override Point MeasureContent(Point available) =>
        new(Math.Min(available.X, (Label is null ? 0 : GameServices.Metrics.Get(Label).Natural) + 4), 1);

    protected override string Render(int width)
    {
        string text = Selected ? $"► {LabelText} ◄" : LabelText;
        int left = Math.Max(0, (width - text.Length) / 2);
        return new string(' ', left) + text;
    }

    public override bool HandleAction(string action)
    {
        if (action != Input.InputMap.Confirm) return false;
        if (!CheckEnabled()) return true;
        Play(Theme.Current.Sounds.Confirm);
        ActionRunner.Run(Actions, new ActionContext(Scene, this, Where));
        return true;
    }
}

/// <summary>A bool variable: ◄ On ►. ui.confirm, ui.left and ui.right flip it.</summary>
public sealed class SwitchControl : ValueControl
{
    public string OnText { get; set; } = "ui.on";
    public string OffText { get; set; } = "ui.off";

    protected override void CheckVariable()
    {
        if (Variable.Type != VarType.Bool) throw new ContentException($"{Where}: a switch needs a bool variable, {Var} is {Value.TypeName(Variable.Type)}");
    }

    protected override int ValueWidth => Math.Max(GameServices.Metrics.Get(OnText).Natural, GameServices.Metrics.Get(OffText).Natural) + 4;
    protected override string ValueText => Arrows(GameServices.T(Current.Bool ? OnText : OffText), ValueWidth - 4);

    public override bool HandleAction(string action)
    {
        if (action is not (Input.InputMap.Confirm or Input.InputMap.Left or Input.InputMap.Right)) return false;
        if (CheckEnabled()) SetValue(Value.Of(!Current.Bool));
        return true;
    }
}

/// <summary>An int variable between min and max: ◄ ███░░ ►. It stops at the limits.</summary>
public sealed class SliderControl : ValueControl
{
    public int? Min { get; set; }
    public int? Max { get; set; }
    public int Step { get; set; } = 1;

    private int Lo => Min ?? Variable.Min ?? 0;
    private int Hi => Max ?? Variable.Max ?? 10;

    protected override void CheckVariable()
    {
        if (Variable.Type != VarType.Int) throw new ContentException($"{Where}: a slider needs an int variable");
        if (Min is null && Variable.Min is null || Max is null && Variable.Max is null)
            throw new ContentException($"{Where}: a slider needs min and max (on the control or on {Var})");
        if (Step <= 0) throw new ContentException($"{Where}: step must be positive");
    }

    private int Cells => Math.Clamp((Hi - Lo) / Step, 1, 20);
    protected override int ValueWidth => Cells + 4;

    protected override string ValueText
    {
        get
        {
            int filled = (int)Math.Round((Current.Int - Lo) / (double)Math.Max(1, Hi - Lo) * Cells);
            return Arrows(new string('█', filled) + new string('░', Cells - filled), Cells);
        }
    }

    public override bool HandleAction(string action)
    {
        int direction = action switch { Input.InputMap.Left => -1, Input.InputMap.Right => 1, _ => 0 };
        if (direction == 0) return false;
        if (!CheckEnabled()) return true;
        int target = Current.Int + direction * Step;
        if (target < Lo || target > Hi) { Play(Theme.Current.Sounds.Locked, 0.5f); return true; }
        SetValue(Value.Of(target));
        return true;
    }
}

/// <summary>
/// Cycles through a list of values: ◄ English ►. Options come from "options" or from the variable's
/// own options (labels: <see cref="LabelPrefix"/> + value, e.g. "lang." + "en").
/// </summary>
public sealed class ComboBoxControl : ValueControl
{
    public List<ComboOption>? Options { get; set; }
    public string? LabelPrefix { get; set; }

    private List<(Value value, string label)> _options = new();

    protected override void CheckVariable()
    {
        if (Options is not null)
        {
            foreach (ComboOption o in Options)
            {
                if (!Value.TryFromJson(o.Value, Variable.Type, out Value v))
                    throw new ContentException($"{Where}: option {o.Value.GetRawText()} is not a {Value.TypeName(Variable.Type)}");
                if (!Variable.Accepts(v, out string reason)) throw new ContentException($"{Where}: option {v}: {reason}");
                _options.Add((v, o.Label));
            }
        }
        else if (Variable.AllowedValues is { } allowed)
            _options = allowed.Select(a => (Value.Of(a), (LabelPrefix ?? "") + a)).ToList();
        else throw new ContentException($"{Where}: a combobox needs 'options', or a variable with options");
        if (_options.Count == 0) throw new ContentException($"{Where}: the combobox has no options");
    }

    private string LabelOf((Value value, string label) o) => GameServices.Loc.Has(o.label) ? GameServices.T(o.label) : o.value.ToString();

    protected override int ValueWidth => _options.Max(o => GameServices.Loc.Has(o.label) ? GameServices.Metrics.Get(o.label).Natural : o.value.ToString().Length) + 4;

    protected override string ValueText
    {
        get
        {
            int index = _options.FindIndex(o => o.value == Current);
            return Arrows(index < 0 ? Current.ToString() : LabelOf(_options[index]), ValueWidth - 4);
        }
    }

    public override bool HandleAction(string action)
    {
        int direction = action switch { Input.InputMap.Left => -1, Input.InputMap.Right or Input.InputMap.Confirm => 1, _ => 0 };
        if (direction == 0) return false;
        if (!CheckEnabled()) return true;
        int index = _options.FindIndex(o => o.value == Current);
        index = ((index < 0 ? 0 : index + direction) % _options.Count + _options.Count) % _options.Count;
        SetValue(_options[index].value);
        return true;
    }
}

public sealed class ComboOption
{
    public System.Text.Json.JsonElement Value { get; set; }
    /// <summary>Text key of the option.</summary>
    public string Label { get; set; } = "";
}
