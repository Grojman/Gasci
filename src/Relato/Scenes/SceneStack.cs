using System.Text.Json;
using System.Text.Json.Nodes;
using Relato.Actions;
using Relato.Blocks;
using Relato.Core;
using Relato.Input;
using Relato.Logic;
using Relato.Variables;

namespace Relato.Scenes;

/// <summary>
/// Root of the screen: every scene of the game (created once at start-up) and the stack of open
/// ones. Only the top scene is enabled (updated, receives input); the scenes below are frozen and
/// drawn only while the scenes above let them show through. Also the glue between actions, events
/// and the game flow (new game, continue, save...).
/// </summary>
public sealed class SceneStack : ScreenObject, IEventHost
{
    private readonly Dictionary<string, Scene> _scenes = new();
    private readonly Dictionary<string, Block> _blocks = new();
    private readonly List<Scene> _stack = new();
    private readonly Dictionary<Scene, Action> _onClosed = new();
    private readonly Queue<Action> _deferred = new();
    private bool _updating;
    private double _autosaveTimer;

    public static SceneStack Instance { get; private set; } = null!;

    public Scene? Top => _stack.Count > 0 ? _stack[^1] : null;
    public IReadOnlyList<Scene> Stack => _stack;
    public IReadOnlyDictionary<string, Scene> Scenes => _scenes;

    /// <summary>Loads every scene file. Block ids must be unique in the whole game.</summary>
    public SceneStack(string folder)
    {
        Instance = this;
        foreach (string file in Directory.EnumerateFiles(folder, "*.json").Order(StringComparer.Ordinal))
        {
            string id = Path.GetFileNameWithoutExtension(file);
            SceneDefinition definition;
            try { definition = Json.Load<SceneDefinition>(file); }
            catch (InvalidDataException e) { throw new ContentException($"scenes/{id}.json: {e.Message}"); }
            var scene = new Scene(id, definition);
            _scenes[id] = scene;
            foreach (Block block in scene.Blocks.Where(b => b.Id is not null))
                if (!_blocks.TryAdd(block.Id!, block))
                    throw new ContentException($"Block id '{block.Id}' is used twice (scenes '{_blocks[block.Id!].Scene.Id}' and '{id}'); ids must be unique in the game");
        }
        Display.GridChanged += () => { foreach (Scene s in _scenes.Values) s.InvalidateLayout(); foreach (Scene s in _stack) s.EnsureLaidOut(); };
    }

    public Scene Get(string id) =>
        _scenes.TryGetValue(id, out Scene? scene) ? scene : throw new ContentException($"There is no scene '{id}' (assets/data/scenes/{id}.json)");

    public Block? FindBlock(string id) => _blocks.GetValueOrDefault(id);

    public void Start(string scene) => ReplaceAll(scene, new());

    // ---- Frame -------------------------------------------------------------------------------

    private bool _wasActive = true;

    public override void Update(TimeSpan delta)
    {
        bool active = Game.Instance.MonoGameInstance.IsActive;
        if (active && !_wasActive) InputMap.IgnoreHeldKeys();
        _wasActive = active;
        InputMap.Update(GameHost.Instance.Keyboard);
        _updating = true;
        base.Update(delta);
        _updating = false;
        Flush();

        Top?.HandleInput();
        Flush();
        GameServices.Events.Update();
        Flush();

        PersistenceScheduler.Tick(delta.TotalSeconds);
        Autosave(delta.TotalSeconds);
    }

    private void Flush()
    {
        while (_deferred.Count > 0) _deferred.Dequeue()();
    }

    /// <summary>Stack changes requested while the scenes update are applied after the update.</summary>
    private void Run(Action change)
    {
        if (_updating) _deferred.Enqueue(change);
        else change();
    }

    private void RefreshStack()
    {
        bool hidden = false;
        for (int i = _stack.Count - 1; i >= 0; i--)
        {
            Scene scene = _stack[i];
            scene.IsEnabled = i == _stack.Count - 1;
            scene.IsVisible = !hidden;
            if (scene.Definition.Presentation.Backdrop == Backdrop.Opaque) hidden = true;
        }
    }

    // ---- Stack -------------------------------------------------------------------------------

    public void Open(string id, Dictionary<string, Value> parameters, Action? closed = null) => Run(() =>
    {
        Scene scene = Get(id);
        if (_stack.Contains(scene)) throw new ContentException($"Scene '{id}' is already open; a scene can only be once in the stack");
        _stack.Add(scene);
        Children.Add(scene);
        if (closed is not null) _onClosed[scene] = closed;
        RefreshStack();
        scene.Open(parameters);
    });

    /// <summary>Closes a scene (the one that ran "back"); the scene below resumes.</summary>
    public void Back(Scene? scene) => Run(() =>
    {
        scene ??= Top;
        if (scene is null || !_stack.Contains(scene)) return;
        if (_stack.Count == 1) { Log.Warn($"'back' in scene '{scene.Id}' ignored: it is the only open scene"); return; }
        Remove(scene);
        RefreshStack();
        Top?.Resume();
        if (_onClosed.Remove(scene, out Action? closed)) closed();
    });

    /// <summary>Replaces a scene by another one in the same place of the stack.</summary>
    public void Goto(Scene? from, string id, Dictionary<string, Value> parameters) => Run(() =>
    {
        from ??= Top;
        Scene target = Get(id);
        int index = from is null ? -1 : _stack.IndexOf(from);
        if (index < 0) { Open(id, parameters); return; }
        if (_stack.Contains(target)) throw new ContentException($"Scene '{id}' is already open");
        from!.Close();
        Children.Remove(from);
        _stack[index] = target;
        Children.Add(target);
        if (_onClosed.Remove(from, out Action? closed)) _onClosed[target] = closed;
        RefreshStack();
        target.Open(parameters);
    });

    /// <summary>Closes every scene above the bottom one.</summary>
    public void CloseAll() => Run(() =>
    {
        while (_stack.Count > 1)
        {
            Scene top = _stack[^1];
            Remove(top);
            if (_onClosed.Remove(top, out Action? closed)) closed();
        }
        RefreshStack();
        Top?.Resume();
    });

    private void Remove(Scene scene)
    {
        _stack.Remove(scene);
        Children.Remove(scene);
        scene.Close();
    }

    private void ReplaceAll(string id, Dictionary<string, Value> parameters, bool restoring = false) => Run(() =>
    {
        foreach (Scene s in _stack.ToList()) Remove(s);
        _onClosed.Clear();
        Open(id, parameters);
    });

    // ---- Game flow ---------------------------------------------------------------------------

    public void NewGame() => Run(() =>
    {
        VariableRegistry.Reset(VarScope.Game);
        GameServices.Events.Reset();
        _autosaveTimer = 0;
        ReplaceAll(GameServices.Game.NewGame.Scene, ActionRunner.Params(GameServices.Game.NewGame.Params));
    });

    public bool ContinueGame()
    {
        SaveGame? save = SaveService.Load();
        if (save is null || save.Scenes.Count == 0) return false;
        Run(() =>
        {
            VariableRegistry.LoadValues(VarScope.Game, save.Variables, "save.json");
            GameServices.Events.Restore(save.CompletedEvents, save.ReadyEvents);
            foreach (Scene s in _stack.ToList()) Remove(s);
            _onClosed.Clear();
            _autosaveTimer = 0;
            foreach (SavedScene saved in save.Scenes)
            {
                Scene scene = Get(saved.Id);
                _stack.Add(scene);
                Children.Add(scene);
                RefreshStack();
                scene.Open(ActionRunner.Params(saved.Params), restoring: true);
                foreach (Block block in scene.Blocks)
                    if (block.Id is not null && save.Blocks.TryGetValue(block.Id, out JsonNode? state)) block.RestoreState(state);
            }
        });
        return true;
    }

    /// <summary>A saved scene is open and no event is running.</summary>
    public bool CanSave => _stack.Any(s => s.Definition.Saved) && !GameServices.Events.IsBusy;
    public bool InGame => _stack.Any(s => s.Definition.Saved);

    /// <summary>Saves the scenes marked "saved", their blocks, the game variables and the events.</summary>
    public bool SaveGame(bool quiet = false)
    {
        if (!CanSave) return false;
        var save = new SaveGame
        {
            Variables = VariableRegistry.Snapshot(VarScope.Game).ToDictionary(v => v.Key, v => JsonSerializer.SerializeToElement(v.Value)),
            CompletedEvents = GameServices.Events.Completed.ToList(),
            ReadyEvents = GameServices.Events.ReadyIds.ToList(),
        };
        foreach (Scene scene in _stack.Where(s => s.Definition.Saved))
        {
            save.Scenes.Add(new SavedScene
            {
                Id = scene.Id,
                Params = scene.Params.ToDictionary(p => p.Key, p => JsonSerializer.SerializeToElement(p.Value.ToJsonObject())),
            });
            foreach (Block block in scene.Blocks)
                if (block.Id is not null && block.SaveState() is { } state) save.Blocks[block.Id] = state;
        }
        try { SaveService.Save(save); }
        catch (Exception e) { Log.Error(e, "Could not write the save file"); return false; }
        if (!quiet) GameServices.Audio?.PlaySfx("save");
        _autosaveTimer = 0;
        return true;
    }

    private void Autosave(double seconds)
    {
        VariableDefinition? enabled = Bindings.VariableFor(Bindings.Autosave);
        if (enabled is null || !VariableRegistry.Read(enabled).Bool || !InGame) { _autosaveTimer = 0; return; }
        int minutes = Bindings.VariableFor(Bindings.AutosaveMinutes) is { } m ? VariableRegistry.Read(m).Int : 5;
        _autosaveTimer += seconds;
        if (_autosaveTimer >= Math.Max(1, minutes) * 60 && CanSave && Top?.Definition.Saved == true) SaveGame(quiet: true);
    }

    public void ReturnToTitle() => Run(() =>
    {
        PersistenceScheduler.FlushAll();
        GameServices.Events.Abort();
        ReplaceAll(GameServices.Game.StartScene, new());
    });

    /// <summary>Opens the quit scene (an exit effect) or closes at once.</summary>
    public void Quit()
    {
        if (GameServices.Game.QuitScene is { } scene) Open(scene, new());
        else Exit();
    }

    public void Exit()
    {
        PersistenceScheduler.FlushAll();
        Game.Instance.MonoGameInstance.Exit();
    }

    public void EndGame()
    {
        SaveService.Delete();
        ReturnToTitle();
    }

    // ---- Targets of actions ------------------------------------------------------------------

    /// <summary>Shows texts in a textbox block. The block must exist and be in the top scene (otherwise nobody could read it).</summary>
    public void ShowMessage(string? target, IReadOnlyList<string> keys, Action? done, string where)
    {
        if (target is null) throw new ContentException($"{where}: 'message' needs a 'target' (the id of a textbox block)");
        if (FindBlock(target) is not TextBoxBlock box)
            throw new ContentException($"{where}: there is no textbox block '{target}'");
        if (box.Scene != Top)
            throw new ContentException($"{where}: textbox '{target}' is in scene '{box.Scene.Id}', which is not the scene on top ('{Top?.Id}'); the text could never be read");
        box.Show(keys, done);
    }

    /// <summary>The map block that owns the player, in the topmost scene that has one.</summary>
    public MapBlock? PlayerMap =>
        _stack.AsEnumerable().Reverse().SelectMany(s => s.Blocks).OfType<MapBlock>().FirstOrDefault(m => m.Player);

    public void Teleport(string map, int x, int y)
    {
        MapBlock playerMap = PlayerMap ?? throw new ContentException("teleport: no open scene has a map block with \"player\": true");
        playerMap.Teleport(map, x, y);
    }

    public bool CanRunEvents => !_updating && Top is { } top && top.CanRunEvents;
}
