using Gasci.Actions;
using Gasci.Config;
using Gasci.Core;
using Gasci.Expressions;
using Gasci.Input;
using Gasci.Logic;
using Gasci.Rendering;
using Gasci.UI;

namespace Gasci.Blocks;

/// <summary>A list of options to choose from, with locked options and an optional countdown.</summary>
public sealed class ChoicesBlock : Block
{
    /// <summary>Options for standalone use (yes/no prompts). A conversation sets its own options.</summary>
    public List<ChoiceItem>? Items { get; set; }

    private List<(string text, Func<bool> locked, Func<double?> countdown)> _options = new();
    private Action<int>? _chosen;
    private int _selected;
    /// <summary>What the surface shows; null = must be redrawn (an empty list must still clear the surface).</summary>
    private string? _rendered;

    [System.Text.Json.Serialization.JsonIgnore] public bool HasOptions => _options.Count > 0;

    public override void OnOpen(bool restoring)
    {
        if (Items is null) { SetOptions([], null); return; }
        var visible = Items.Where(i => Expression.IsTrue(i.If)).ToList();
        SetOptions(visible.Select(i => (i.Text, (Func<bool>)(() => i.LockedIf is not null && Expression.IsTrue(i.LockedIf)), (Func<double?>)(() => null))).ToList(),
            index => ActionRunner.Run(visible[index].Actions, new ActionContext(Scene, this, Where)));
    }

    public void SetOptions(List<(string text, Func<bool> locked, Func<double?> countdown)> options, Action<int>? chosen)
    {
        _options = options;
        _chosen = chosen;
        _selected = Math.Max(0, _options.FindIndex(o => !o.locked()));
        _rendered = null;
    }

    protected override Point MeasureContent(Point available)
    {
        IEnumerable<string> texts = Items?.Select(i => i.Text) ?? [];
        int width = texts.Select(t => GameServices.Metrics.Get(t).Natural + 4).DefaultIfEmpty(20).Max();
        return new Point(Math.Min(width, available.X), Math.Max(1, Items?.Count ?? 4));
    }

    protected override void OnArrange()
    {
        EnsureSurface(Background is null ? Color.Black : BackgroundColor);
        _rendered = null;
    }

    public override bool HandleAction(string action)
    {
        if (_options.Count == 0) return false;
        switch (action)
        {
            case InputMap.Up: Move(-1); return true;
            case InputMap.Down: Move(1); return true;
            case InputMap.Confirm:
                if (_options[_selected].locked()) { GameServices.Audio?.PlaySfx(Theme.Current.Sounds.Locked ?? "locked"); return true; }
                _chosen?.Invoke(_selected);
                return true;
        }
        return false;
    }

    private void Move(int direction)
    {
        _selected = (_selected + direction + _options.Count) % _options.Count;
        if (Theme.Current.Sounds.Move is { } sfx) GameServices.Audio?.PlaySfx(sfx, 0.6f);
    }

    public override void Update(TimeSpan delta)
    {
        if (Surface is null || !IsShown) return;
        Theme theme = Theme.Current;
        var lines = new List<(string, Color)>();
        for (int i = 0; i < _options.Count; i++)
        {
            var (text, locked, countdown) = _options[i];
            bool isLocked = locked(), selected = i == _selected;
            string label = GameServices.T(text);
            if (isLocked) label += "  " + GameServices.T("dialog.locked");
            else if (countdown() is { } seconds) label += "  " + new string('·', Math.Max(0, (int)Math.Ceiling(seconds))); // time running out
            Color color = isLocked ? theme.Disabled : selected ? theme.Highlight : theme.Text;
            lines.Add(((selected ? "► " : "  ") + label, color));
        }
        string key = string.Join("|", lines.Select(l => l.Item1 + l.Item2.PackedValue));
        if (key == _rendered) return;
        _rendered = key;
        ICellSurface s = Surface.Surface;
        s.Clear();
        for (int i = 0; i < lines.Count && i < s.Height; i++) Frame.Print(s, 3, i, lines[i].Item1, lines[i].Item2);
    }
}

public sealed class ChoiceItem
{
    public string Text { get; set; } = "";
    public string? If { get; set; }
    public string? LockedIf { get; set; }
    public List<ActionDefinition> Actions { get; set; } = new();
}

/// <summary>
/// Plays a conversation of conversations.json. It drives three slot blocks that are laid out like
/// any other block: a portrait image, a text box and a choices list. When the conversation ends it
/// runs <see cref="OnEnd"/> (by default: close the scene).
/// </summary>
public sealed class ConversationBlock : Block
{
    /// <summary>Conversation id or scene parameter ("$conversation").</summary>
    public string? Conversation { get; set; }
    public ImageBlock Portrait { get; set; } = new();
    public TextBoxBlock Text { get; set; } = new();
    public ChoicesBlock Options { get; set; } = new();
    public List<ActionDefinition> OnEnd { get; set; } = [new() { Type = ActionTypes.Back }];

    private Logic.Conversation _conversation = null!;
    private ConversationNode _node = null!;
    private List<DialogOption>? _choices;
    private double _nodeTime, _choosingTime;
    private bool _imageChanged, _ended;
    private string? _previousMusic;
    private string _color = "";

    [System.Text.Json.Serialization.JsonIgnore] public override IEnumerable<Block> Children => [Portrait, Text, Options];

    protected override void OnAttach()
    {
        Text.AutoHide = false;
        Text.TakeFocus = false;
    }

    protected override Point MeasureContent(Point available) => available;

    protected override void OnArrange() => OverlayBlock.ArrangeOverlay(Bounds, Children.Where(c => c.IsShown));

    public override void OnOpen(bool restoring)
    {
        string id = Resolve(Conversation) ?? throw new ContentException($"{Where}: missing 'conversation'");
        _conversation = GameServices.Conversations.Get(id);
        _color = _conversation.Color ?? "#d2c8be";
        _ended = false;
        _previousMusic = GameServices.Audio?.CurrentMusic;
        if (_conversation.Music is { } music) GameServices.Audio?.PlayMusic(music);
        Portrait.Clear();
        EnterNode(_conversation.Start);
    }

    private string Where2 => $"conversation '{Resolve(Conversation)}'";

    private void EnterNode(string? id)
    {
        if (id is null || !_conversation.Nodes.TryGetValue(id, out ConversationNode? node))
        {
            End();
            return;
        }
        _node = node;
        _nodeTime = 0;
        _imageChanged = false;
        _choices = null;
        Options.SetOptions([], null);

        ActionRunner.Apply(node.Set, node.Add, $"{Where2}, node '{id}'");
        if (node.Sfx is not null) GameServices.Audio?.PlaySfx(node.Sfx);
        if (node.Image is { } image) DrawPortrait(image, node.DrawMode, node.CursorMode);

        if (node.Text is not null) Text.ShowKey(node.Text);
        else AfterText();
    }

    private List<DialogOption> VisibleOptions() =>
        _node.Options?.Where(o => Expression.IsTrue(o.If)).ToList() ?? [];

    private void AfterText()
    {
        List<DialogOption> visible = VisibleOptions();
        if (visible.Count > 0)
        {
            _choices = visible;
            _choosingTime = 0;
            Options.SetOptions(visible.Select(o => (o.Text, (Func<bool>)(() => IsLocked(o)),
                (Func<double?>)(() => o.LockAfter is { } s ? s - _choosingTime : null))).ToList(), i => Choose(visible[i]));
            return;
        }
        string? next = _node.Branch?.FirstOrDefault(b => Expression.IsTrue(b.If))?.Next ?? _node.Next;
        EnterNode(next);
    }

    private void Choose(DialogOption option)
    {
        GameServices.Audio?.PlaySfx(option.Sfx ?? Theme.Current.Sounds.Confirm ?? "confirm");
        ActionRunner.Apply(option.Set, option.Add, $"{Where2}, option '{option.Text}'");
        _choices = null;
        EnterNode(option.Next);
    }

    private bool IsLocked(DialogOption option) =>
        (option.LockedIf is not null && Expression.IsTrue(option.LockedIf))
        || (option.LockAfter is { } seconds && _choosingTime >= seconds);

    private void End()
    {
        if (_ended) return;
        _ended = true;
        Portrait.Complete();
        if (_conversation.Music is not null) GameServices.Audio?.PlayMusic(_previousMusic);
        ActionRunner.Run(OnEnd, new ActionContext(Scene, this, Where));
    }

    private void DrawPortrait(int frame, string? mode, string? cursorMode) =>
        Portrait.Show(GameServices.Images.Character(_conversation.Character, frame),
            mode ?? _conversation.DrawMode, cursorMode ?? _conversation.CursorMode, _color);

    public override void Update(TimeSpan delta)
    {
        if (_ended || _node is null) return;
        _nodeTime += delta.TotalSeconds;
        if (_choices is not null) _choosingTime += delta.TotalSeconds;

        // A question shows its answers as soon as it has been fully written.
        if (_choices is null && Text.IsFinished && VisibleOptions().Count > 0) AfterText();
        Text.SetIndicateEnd(_choices is null);

        if (_node.ImageAfter is { } change && !_imageChanged && _nodeTime >= change.Seconds)
        {
            _imageChanged = true;
            DrawPortrait(change.Image, change.DrawMode, change.CursorMode);
        }
    }

    public override bool HandleAction(string action)
    {
        if (_ended) return true;
        if (_choices is not null) return Options.HandleAction(action) || action == InputMap.Confirm;
        if (action != InputMap.Confirm) return false;
        if (Portrait.IsDrawing) { Portrait.Complete(); return true; }
        if (Text.Advance()) AfterText();
        return true;
    }
}

/// <summary>
/// A dramatic card (the "black" event action): an image and/or a line of text, closed after a
/// number of milliseconds or when the player confirms. Scene parameters: key, image, ms, big,
/// drawMode, cursorMode. Slots: image, text (used with an image) and soloText (used without).
/// </summary>
public sealed class CardBlock : Block
{
    public ImageBlock Image { get; set; } = new();
    public TextBoxBlock Text { get; set; } = new();
    public TextBoxBlock SoloText { get; set; } = new();
    public List<ActionDefinition> OnEnd { get; set; } = [new() { Type = ActionTypes.Back }];

    private string? _key;
    private double _seconds, _finishedTime;
    private bool _closed, _hasImage, _textShown;
    private TextBoxBlock _text = null!;

    [System.Text.Json.Serialization.JsonIgnore] public override IEnumerable<Block> Children => [Image, Text, SoloText];

    protected override void OnAttach()
    {
        foreach (TextBoxBlock t in new[] { Text, SoloText }) { t.AutoHide = true; t.TakeFocus = false; t.IndicateEnd = false; }
    }

    protected override Point MeasureContent(Point available) => available;

    protected override void OnArrange() => OverlayBlock.ArrangeOverlay(Bounds, Children.Where(c => c.IsShown));

    public override void OnOpen(bool restoring)
    {
        _closed = false;
        _textShown = false;
        _finishedTime = 0;
        _key = Resolve("$key") is { Length: > 0 } k ? k : null;
        _seconds = (int.TryParse(Resolve("$ms"), out int ms) ? ms : 0) / 1000.0;
        bool big = Resolve("$big") == "true";
        string? image = Resolve("$image") is { Length: > 0 } i ? i : null;
        _hasImage = image is not null;

        Image.SetVisible(_hasImage);
        Text.SetVisible(false);
        SoloText.SetVisible(false);
        _text = _hasImage ? Text : SoloText;
        if (_text.Scale != (big ? 2 : 1)) { _text.Scale = big ? 2 : 1; Scene.InvalidateLayout(); }

        if (_hasImage)
            Image.Show(GameServices.Images.Get(image!), NullIfEmpty(Resolve("$drawMode")) ?? "Inside", NullIfEmpty(Resolve("$cursorMode")), Image.Color);
        else ShowText();
    }

    private void ShowText()
    {
        if (_textShown || _key is null) return;
        _textShown = true;
        _text.ShowKey(_key);
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;

    private bool IsFinished => Image.IsFinished && (_key is null || (_textShown && _text.IsFinished));

    public override void Update(TimeSpan delta)
    {
        if (_closed) return;
        if (_hasImage && !Image.IsFinished) return;
        if (!_textShown && _key is not null) { ShowText(); return; }
        if (IsFinished && _seconds > 0)
        {
            _finishedTime += delta.TotalSeconds;
            if (_finishedTime >= _seconds) Close();
        }
    }

    public override bool HandleAction(string action)
    {
        if (_closed || action != InputMap.Confirm) return action == InputMap.Back;
        if (Image.IsDrawing)
        {
            Image.Complete();
            ShowText();
            return true;
        }
        if (!_textShown && _key is not null) { ShowText(); return true; }
        if (!_text.Advance()) return true;
        Close();
        return true;
    }

    private void Close()
    {
        _closed = true;
        ActionRunner.Run(OnEnd, new ActionContext(Scene, this, Where));
    }
}
