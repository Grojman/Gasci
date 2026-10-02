using Gasci.Blocks.Controls;
using Gasci.Config;
using Gasci.Core;
using Gasci.Input;

namespace Gasci.Blocks;

/// <summary>
/// A stack of controls with a selection. Up/down (left/right with "navigation": "horizontal") move
/// the selection over the visible, enabled controls; every other action goes to the selected control.
/// The selection is kept when the scene is resumed (returning from settings keeps "Settings" selected).
/// </summary>
public sealed class MenuBlock : StackBlock
{
    public string Navigation { get; set; } = "vertical";

    private int _selected = -1;

    private List<Control> Controls => Items.OfType<Control>().Where(c => c.IsShown).ToList();

    [System.Text.Json.Serialization.JsonIgnore] public Control? Selected => _selected >= 0 && _selected < Controls.Count ? Controls[_selected] : null;

    protected override void OnAttach()
    {
        foreach (Block child in Items)
            if (child is not Control) throw new ContentException($"{Where}: a menu can only hold controls (button, switch, slider...), found {child.Name}");
    }

    public override void OnOpen(bool restoring) => SelectFirstEnabled();

    public override void OnResume()
    {
        if (Selected is not { IsEnabled: true }) SelectFirstEnabled();
    }

    private void SelectFirstEnabled()
    {
        List<Control> controls = Controls;
        _selected = Math.Max(0, controls.FindIndex(c => c.IsEnabled));
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        List<Control> controls = Controls;
        for (int i = 0; i < controls.Count; i++) controls[i].Selected = i == _selected;
    }

    protected override Point MeasureContent(Point available)
    {
        var labelled = Items.OfType<Control>().Where(c => c.HasLabelColumn).ToList();
        int column = labelled.Select(c => c.LabelWidth).DefaultIfEmpty(0).Max();
        foreach (Control c in labelled) c.LabelColumn = column;
        return base.MeasureContent(available);
    }

    public override bool HandleAction(string action)
    {
        List<Control> controls = Controls;
        if (controls.Count == 0) return false;
        if (_selected >= controls.Count || _selected < 0) SelectFirstEnabled();
        Control selected = controls[_selected];
        if (selected.IsCapturing) return true;

        bool horizontal = Navigation == "horizontal";
        string previous = horizontal ? InputMap.Left : InputMap.Up, next = horizontal ? InputMap.Right : InputMap.Down;
        if (action == previous) { Move(-1); return true; }
        if (action == next) { Move(1); return true; }
        return selected.HandleAction(action);
    }

    private void Move(int direction)
    {
        List<Control> controls = Controls;
        for (int i = 1; i <= controls.Count; i++)
        {
            int candidate = ((_selected + direction * i) % controls.Count + controls.Count) % controls.Count;
            if (!controls[candidate].IsEnabled) continue;
            _selected = candidate;
            UpdateSelection();
            if (Theme.Current.Sounds.Move is { } sfx) GameServices.Audio?.PlaySfx(sfx, 0.6f);
            return;
        }
    }

    public override void Update(TimeSpan delta) => UpdateSelection();
}
