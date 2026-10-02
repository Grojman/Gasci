using System.Text.Json.Serialization;
using Gasci.Config;
using Gasci.Core;
using Gasci.Input;
using Gasci.Variables;
using SadConsole.Input;

namespace Gasci.Blocks.Controls;

/// <summary>Common part of the controls that capture the raw keyboard while editing.</summary>
public abstract class EditingControl : ValueControl
{
    [JsonIgnore] protected bool Editing { get; private set; }
    [JsonIgnore] protected string Buffer { get; set; } = "";

    public override bool IsCapturing => Editing;

    protected void BeginEdit(string initial)
    {
        Editing = true;
        Buffer = initial;
        InputMap.Capture(this);
    }

    protected void EndEdit()
    {
        Editing = false;
        InputMap.ReleaseCapture(this);
    }

    public override void OnClose()
    {
        if (Editing) EndEdit();
    }

    public override void Update(TimeSpan delta)
    {
        if (Editing && InputMap.Keyboard is { } keyboard && Scene.IsTop)
        {
            foreach (AsciiKey key in keyboard.KeysPressed)
            {
                if (key.Key == Keys.Enter) { Commit(); break; }
                if (key.Key == Keys.Escape) { EndEdit(); Play(Theme.Current.Sounds.Move, 0.6f); break; }
                if (key.Key == Keys.Back) { if (Buffer.Length > 0) Buffer = Buffer[..^1]; continue; }
                if (key.Character != '\0' && !char.IsControl(key.Character)) Type(key.Character);
            }
        }
        base.Update(delta);
    }

    /// <summary>A printable character was typed.</summary>
    protected abstract void Type(char c);
    /// <summary>Enter while editing.</summary>
    protected abstract void Commit();
}

/// <summary>An int variable: ◄ 12 ►. Left/right change it by step; ui.confirm starts typing digits.</summary>
public sealed class NumberFieldControl : EditingControl
{
    public int Step { get; set; } = 1;

    private int Lo => Variable.Min ?? int.MinValue;
    private int Hi => Variable.Max ?? int.MaxValue;

    protected override void CheckVariable()
    {
        if (Variable.Type != VarType.Int) throw new ContentException($"{Where}: a numberfield needs an int variable");
        if (Step <= 0) throw new ContentException($"{Where}: step must be positive");
    }

    protected override int ValueWidth => Math.Max(Variable.MaxTextWidth, 1) + 4;

    protected override string ValueText => Editing
        ? "[" + (Buffer + "_").PadRight(ValueWidth - 2) + "]"
        : Arrows(Current.Int.ToString(), ValueWidth - 4);

    public override bool HandleAction(string action)
    {
        if (action == InputMap.Confirm) { if (CheckEnabled()) BeginEdit(""); return true; }
        int direction = action switch { InputMap.Left => -1, InputMap.Right => 1, _ => 0 };
        if (direction == 0) return false;
        if (!CheckEnabled()) return true;
        long target = (long)Current.Int + direction * Step;
        if (target < Lo || target > Hi) { Play(Theme.Current.Sounds.Locked, 0.5f); return true; }
        SetValue(Value.Of((int)target));
        return true;
    }

    protected override void Type(char c)
    {
        string candidate = Buffer + c;
        bool accepted = (char.IsDigit(c) || (c == '-' && Buffer.Length == 0 && Lo < 0))
            && candidate.Length <= Variable.MaxTextWidth
            && (candidate == "-" || (long.TryParse(candidate, out long v) && v <= Hi && v >= Math.Min(Lo, 0)));
        if (accepted) Buffer = candidate;
        else Play(Theme.Current.Sounds.Locked, 0.5f); // the digit would leave the allowed range: refused on screen
    }

    protected override void Commit()
    {
        if (!int.TryParse(Buffer, out int value) || value < Lo || value > Hi)
        {
            Play(Theme.Current.Sounds.Locked);
            return; // stays editing: the player sees the value is not accepted
        }
        EndEdit();
        SetValue(Value.Of(value));
    }
}

/// <summary>
/// A string variable. ui.confirm starts editing: typed characters are added until maxLength (then
/// refused with the locked sound, and the counter shows the limit); Enter confirms, Escape cancels.
/// </summary>
public sealed class TextFieldControl : EditingControl
{
    protected override void CheckVariable()
    {
        if (Variable.Type != VarType.String) throw new ContentException($"{Where}: a textfield needs a string variable");
        if (Variable.MaxLength is null) throw new ContentException($"{Where}: {Var} needs a maxLength to be edited in a text field");
    }

    private int Max => Variable.MaxLength!.Value;
    protected override int ValueWidth => Max + 2 + 1 + $"{Max}/{Max}".Length;

    protected override string ValueText
    {
        get
        {
            string text = Editing ? Buffer + (Buffer.Length < Max ? "_" : "") : Current.Str;
            string counter = Editing ? $" {Buffer.Length}/{Max}" : "";
            return "[" + text.PadRight(Max) + "]" + counter;
        }
    }

    public override bool HandleAction(string action)
    {
        if (action != InputMap.Confirm) return false;
        if (CheckEnabled()) BeginEdit(Current.Str);
        return true;
    }

    protected override void Type(char c)
    {
        if (Buffer.Length >= Max || !CharMap.Current.Has(c)) { Play(Theme.Current.Sounds.Locked, 0.5f); return; }
        Buffer += c;
    }

    protected override void Commit()
    {
        if (!Variable.Accepts(Value.Of(Buffer), out _)) { Play(Theme.Current.Sounds.Locked); return; }
        EndEdit();
        SetValue(Value.Of(Buffer));
    }
}

/// <summary>
/// Shows the keys of an input action, one column per slot (primary, secondary...). Left/right choose
/// the slot; ui.confirm (Enter) starts listening and the next key pressed is bound to the slot.
/// Enter again cancels. A key already used by another action of the same context is swapped.
/// </summary>
public sealed class KeyBindControl : Control
{
    /// <summary>Input action id ("map.up").</summary>
    public string Action { get; set; } = "";

    private int _slot;
    private bool _listening;
    private string? _status;
    private double _statusTime;
    private const int SlotWidth = 10;

    public override bool IsCapturing => _listening;

    protected override void OnAttach()
    {
        if (!InputMap.Definitions.TryGetValue(Action, out InputActionDefinition? def))
            throw new ContentException($"{Where}: unknown input action '{Action}'");
        if (!def.Rebindable) throw new ContentException($"{Where}: '{Action}' is not rebindable");
        Label ??= def.Label;
    }

    internal override bool HasLabelColumn => true;
    internal override int LabelWidth => Label is null ? Action.Length : GameServices.Metrics.Get(Label).Natural;

    protected override Point MeasureContent(Point available) =>
        new(Math.Min(available.X, Math.Max(LabelColumn, LabelWidth) + 2 + InputMap.MaxKeys * (SlotWidth + 1) + 16), 1);

    protected override string Render(int width)
    {
        var line = new System.Text.StringBuilder((Label is null ? Action : LabelText).PadRight(Math.Max(LabelColumn, LabelWidth) + 2));
        for (int i = 0; i < InputMap.MaxKeys; i++)
        {
            string name = _listening && i == _slot ? "..." : InputMap.KeyAt(Action, i) is { } k ? InputMap.KeyName(k) : "-";
            string cell = Selected && i == _slot ? $"[{name}]" : $" {name} ";
            line.Append(cell.PadRight(SlotWidth + 1));
        }
        if (_status is not null) line.Append(_status);
        return line.ToString().PadRight(width);
    }

    public override bool HandleAction(string action)
    {
        switch (action)
        {
            case InputMap.Left: _slot = Math.Max(0, _slot - 1); return true;
            case InputMap.Right: _slot = Math.Min(InputMap.MaxKeys - 1, _slot + 1); return true;
            case InputMap.Confirm:
                if (!CheckEnabled()) return true;
                _listening = true;
                _status = null;
                InputMap.Capture(this); // from the next frame: the Enter that started it is not bound
                return true;
        }
        return false;
    }

    public override void OnClose() => StopListening();

    private void StopListening()
    {
        _listening = false;
        InputMap.ReleaseCapture(this);
    }

    public override void Update(TimeSpan delta)
    {
        if (_status is not null && (_statusTime -= delta.TotalSeconds) <= 0) _status = null;
        if (_listening && InputMap.Keyboard is { } keyboard && Scene.IsTop)
        {
            foreach (AsciiKey key in keyboard.KeysPressed)
            {
                StopListening();
                if (key.Key == InputMap.FixedConfirmKey) break; // Enter cancels
                string? swapped = InputMap.Bind(Action, _slot, key.Key);
                Play(Theme.Current.Sounds.Confirm);
                if (swapped is not null && swapped != Action)
                {
                    string other = InputMap.Definitions[swapped].Label is { } l ? GameServices.T(l) : swapped;
                    _status = $"↔ {other}";
                    _statusTime = 2.5;
                }
                NotifyChanged();
                break;
            }
        }
        base.Update(delta);
    }
}
