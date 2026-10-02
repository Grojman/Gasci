using System.Text.RegularExpressions;
using Relato.Actions;
using Relato.Blocks;
using Relato.Blocks.Controls;
using Relato.Expressions;
using Relato.Input;
using Relato.Layout;
using Relato.Logic;
using Relato.Maps;
using Relato.Rendering;
using Relato.Scenes;
using Relato.Variables;

namespace Relato.Core;

/// <summary>
/// `dotnet run -- --validate`: loads every asset and checks the references between them (text keys,
/// variables and their types, expressions, scenes and blocks, actions, events, maps, conversations,
/// images, input actions, the custom font) without opening a window. It also regenerates
/// assets/data/generated/text_metrics.csv. It is a check to run before a build, so it may be slow.
/// Returns 1 if something is wrong.
/// </summary>
public static class ContentValidator
{
    private static readonly List<string> Errors = new();
    private static readonly List<string> Warnings = new();
    private static readonly HashSet<string> UsedVariables = new();

    public static int Run()
    {
        Log.FilePath = null;
        SceneStack stack;
        try
        {
            GameServices.LoadContent();
            Display.Configure(GameServices.Game.Grid, GameServices.Font?.GlyphSize ?? new Point(8, 16));
            stack = new SceneStack(Paths.Scenes);
        }
        catch (Exception e)
        {
            System.Console.WriteLine($"FAILED to load content: {e.Message}");
            return 1;
        }

        CheckGame(stack);
        CheckTexts();
        CheckInput();
        CheckMaps(stack);
        CheckConversations();
        CheckEvents(stack);
        CheckScenes(stack);
        CheckUnusedVariables();
        WriteTextMetrics();

        foreach (string w in Warnings) System.Console.WriteLine($"warning: {w}");
        foreach (string e in Errors) System.Console.WriteLine($"error:   {e}");
        System.Console.WriteLine(Errors.Count == 0
            ? $"Content OK ({GameServices.Loc.Keys.Count()} texts, {VariableRegistry.Definitions.Count()} variables, " +
              $"{stack.Scenes.Count} scenes, {GameServices.Maps.AvailableMaps.Count()} maps, " +
              $"{GameServices.Conversations.All.Count} conversations, {GameServices.Events.Definitions.Count} events, " +
              $"{InputMap.Definitions.Count} input actions)"
            : $"{Errors.Count} error(s)");
        return Errors.Count == 0 ? 0 : 1;
    }

    private static void Error(string message) => Errors.Add(message);
    private static void Warn(string message) => Warnings.Add(message);

    private static void Key(string? key, string where)
    {
        if (key is null || key.StartsWith('$')) return;
        if (!GameServices.Loc.Has(key)) { Error($"{where}: missing text key '{key}'"); return; }
        foreach (string language in GameServices.Loc.Languages)
            if (!GameServices.Loc.HasLanguage(key, language)) Warn($"{where}: '{key}' has no '{language}' translation");
    }

    /// <summary>Compiles an expression, records the variables it reads, and checks its type.</summary>
    private static Expression? Expr(string? source, string where, VarType? type = null, bool condition = false)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;
        try
        {
            Expression e = condition ? Expression.Condition(source) : Expression.Compile(source);
            UsedVariables.UnionWith(e.Variables);
            if (type is { } t && e.Type != t) Error($"{where}: '{source}' is a {Value.TypeName(e.Type)}, a {Value.TypeName(t)} is needed");
            return e;
        }
        catch (ExpressionException e) { Error($"{where}: {e.Message}"); return null; }
    }

    private static void Cond(string? source, string where) => Expr(source, where, condition: true);

    private static VariableDefinition? Variable(string? name, string where, bool write = false)
    {
        if (name is null) { Error($"{where}: missing variable name"); return null; }
        UsedVariables.Add(name);
        if (VariableRegistry.Find(name) is not { } def) { Error($"{where}: {VariableRegistry.Undeclared(name)}"); return null; }
        if (write && def.IsReadOnly) Error($"{where}: {name} is read-only");
        return def;
    }

    // ---- Game ----------------------------------------------------------------------------

    private static void CheckGame(SceneStack stack)
    {
        var game = GameServices.Game;
        if (game.Grid.MinWidth < 20 || game.Grid.MinHeight < 10) Error("game.json: the minimum grid must be at least 20x10");
        if (game.Grid.FontScales.Count == 0) Error("game.json: grid.fontScales is empty");
        foreach (int s in game.Grid.FontScales) if (s is < 1 or > 4) Error($"game.json: font scale {s} is not supported (1-4)");
        if (game.Images.MinCharSize is not [> 0, > 0]) Error("game.json: images.minCharSize must be two positive numbers");
        SceneExists(stack, game.StartScene, "game.json startScene", new());
        SceneExists(stack, game.NewGame.Scene, "game.json newGame", game.NewGame.Params.ToDictionary(p => p.Key, p => Value.JsonType(p.Value)));
        SceneExists(stack, game.ConversationScene, "game.json conversationScene", new() { ["conversation"] = VarType.String });
        SceneExists(stack, game.CardScene, "game.json cardScene", new()
        {
            ["key"] = VarType.String, ["image"] = VarType.String, ["ms"] = VarType.Int, ["big"] = VarType.Bool,
            ["drawMode"] = VarType.String, ["cursorMode"] = VarType.String,
        });
        if (game.QuitScene is { } quit) SceneExists(stack, quit, "game.json quitScene", new());
        if (!stack.Scenes.Values.Any(s => s.Definition.Saved)) Warn("no scene has \"saved\": true, so the game can never be saved");
        foreach (string binding in Bindings.Known.Keys)
            if (Bindings.VariableFor(binding) is null) Warn($"no settings variable is bound to '{binding}', so the player cannot change it");
        if (game.Font is not null && GameServices.Font is { Problems.Count: > 0 } font)
            foreach (string p in font.Problems) Error($"font: {p}");
    }

    private static void SceneExists(SceneStack stack, string id, string where, Dictionary<string, VarType?> passed)
    {
        if (!stack.Scenes.TryGetValue(id, out Scene? scene)) { Error($"{where}: there is no scene '{id}'"); return; }
        foreach (var (name, type) in scene.Definition.Params)
        {
            if (!passed.TryGetValue(name, out VarType? given)) Error($"{where}: scene '{id}' needs the parameter '{name}' ({type})");
            else if (given is { } g && Value.TypeName(g) != type) Error($"{where}: parameter '{name}' of scene '{id}' must be {type}, it gets {Value.TypeName(g)}");
        }
        foreach (string name in passed.Keys)
            if (!scene.Definition.Params.ContainsKey(name)) Error($"{where}: scene '{id}' has no parameter '{name}'");
    }

    // ---- Texts and input -----------------------------------------------------------------

    private static readonly Regex VariableCode = new(@"\{v:([^}]*)\}");

    private static void CheckTexts()
    {
        Localization loc = GameServices.Loc;
        foreach (string key in loc.Keys)
            foreach (string language in loc.Languages)
            {
                if (!loc.HasLanguage(key, language)) continue;
                string text = loc.Get(key, language);
                foreach (Match m in VariableCode.Matches(text))
                {
                    if (Variable(m.Groups[1].Value, $"text '{key}' ({language})") is { } def && def.MaxTextWidth == 0)
                        Error($"text '{key}': {{v:{def.Name}}} needs a variable with a maxLength or options so its width is known");
                }
                foreach (char c in TextLayout.Strip(text))
                    if (c != '\n' && !CharMap.Current.Has(c)) Warn($"text '{key}' ({language}): character '{c}' (U+{(int)c:X4}) is not in the font");
            }
    }

    private static void CheckInput()
    {
        foreach (var (id, def) in InputMap.Definitions)
            if (def.Rebindable) Key(def.Label, $"input action '{id}' label");
    }

    // ---- Actions -------------------------------------------------------------------------

    private static void Actions(IEnumerable<ActionDefinition>? actions, string where, SceneStack stack, Scene? scene)
    {
        if (actions is null) return;
        int i = 0;
        foreach (ActionDefinition a in actions)
        {
            string w = $"{where}, action {i++} ({a.Type})";
            Cond(a.If, w);
            if (!ActionTypes.All.Contains(a.Type)) { Error($"{w}: unknown action type (known: {string.Join(", ", ActionTypes.All)})"); continue; }
            switch (a.Type)
            {
                case ActionTypes.Set or ActionTypes.Add:
                    if (Variable(a.Var, w, write: true) is not { } def) break;
                    if (a.Type == ActionTypes.Add && def.Type != VarType.Int) Error($"{w}: 'add' needs an int variable, {def.Name} is {Value.TypeName(def.Type)}");
                    if (a.Expr is not null)
                    {
                        if (Expr(a.Expr, w, a.Type == ActionTypes.Add ? VarType.Int : def.Type) is { UsesRandom: true }) { /* allowed in values */ }
                    }
                    else if (a.Value is not { } json || !Value.TryFromJson(json, a.Type == ActionTypes.Add ? VarType.Int : def.Type, out Value v))
                        Error($"{w}: needs a {Value.TypeName(def.Type)} 'value' or an 'expr'");
                    else if (a.Type == ActionTypes.Set && !def.Accepts(v, out string reason)) Warn($"{w}: {reason}");
                    break;
                case ActionTypes.Open or ActionTypes.Goto:
                    if (a.Scene is null) { Error($"{w}: missing 'scene'"); break; }
                    SceneExists(stack, a.Scene, w, (a.Params ?? new()).ToDictionary(p => p.Key, p => Value.JsonType(p.Value)));
                    break;
                case ActionTypes.Focus:
                    if (a.Mode is not (null or "move" or "share" or "release")) Error($"{w}: unknown focus mode '{a.Mode}'");
                    if (a.Target is not null)
                    {
                        if (stack.FindBlock(a.Target) is not { } target) Error($"{w}: there is no block '{a.Target}'");
                        else if (scene is not null && target.Scene != scene) Error($"{w}: block '{a.Target}' is in another scene ('{target.Scene.Id}')");
                    }
                    else if (a.Mode is null or "move" or "share") Error($"{w}: needs a 'target'");
                    break;
                case ActionTypes.Message:
                    if (a.Target is null) Error($"{w}: needs a 'target' (the id of a textbox block)");
                    else if (stack.FindBlock(a.Target) is not TextBoxBlock) Error($"{w}: '{a.Target}' is not a textbox block");
                    foreach (string key in a.Keys ?? (a.Key is null ? [] : [a.Key])) Key(key, w);
                    break;
                case ActionTypes.Dialog:
                    if (a.Id is null || !GameServices.Conversations.All.ContainsKey(a.Id)) Error($"{w}: unknown conversation '{a.Id}'");
                    break;
                case ActionTypes.Black:
                    Key(a.Key, w);
                    if (a.Image is not null && !GameServices.Images.Exists(a.Image)) Error($"{w}: image '{a.Image}' does not exist");
                    if (a.DrawMode is not null && !Enum.TryParse<DrawMode>(a.DrawMode, true, out _)) Error($"{w}: unknown drawMode '{a.DrawMode}'");
                    break;
                case ActionTypes.Teleport:
                    if (!GameServices.Maps.AvailableMaps.Contains(a.Map ?? "")) Error($"{w}: unknown map '{a.Map}'");
                    break;
                case ActionTypes.Sfx:
                    if (string.IsNullOrEmpty(a.Id)) Error($"{w}: missing 'id'");
                    break;
                case ActionTypes.ResetBinding:
                    if (a.Action is null || !InputMap.Definitions.ContainsKey(a.Action)) Error($"{w}: unknown input action '{a.Action}'");
                    break;
                case ActionTypes.Save:
                    Actions(a.OnSuccess, w + " onSuccess", stack, scene);
                    Actions(a.OnFail, w + " onFail", stack, scene);
                    break;
            }
        }
    }

    // ---- Maps ----------------------------------------------------------------------------

    private static void CheckMaps(SceneStack stack)
    {
        Point section = new(GameServices.Game.Map.SectionWidth, GameServices.Game.Map.SectionHeight);
        foreach (string id in GameServices.Maps.AvailableMaps)
        {
            string where = $"map '{id}'";
            MapInfo info;
            MapData baseMap;
            try
            {
                info = GameServices.Maps.Info(id);
                baseMap = GameServices.Maps.LoadFile(id, id, info);
            }
            catch (Exception e) { Error($"{where}: {e.Message}"); continue; }

            Key(info.TitleKey, where);
            void Textbox(string? target, string w)
            {
                if (target is null) Error($"{w}: has an interaction text but no 'textbox' (on it or on the map) to show it in");
                else if (stack.FindBlock(target) is not TextBoxBlock) Error($"{w}: '{target}' is not a textbox block");
            }

            foreach (string file in info.Variants.Select(v => v.File).Prepend(id))
            {
                MapData map;
                try { map = GameServices.Maps.LoadFile(id, file, info); }
                catch (Exception e) { Error($"{where}: cannot load '{file}.txt': {e.Message}"); continue; }
                if (map.Width != baseMap.Width || map.Height != baseMap.Height)
                    Error($"{where}: variant '{file}' is {map.Width}x{map.Height}, base is {baseMap.Width}x{baseMap.Height}");
                for (int y = 0; y < map.Height; y++)
                    for (int x = 0; x < map.Width; x++)
                        if (map.TileAt(x, y) is null && !CharMap.Current.Has(map.CharAt(x, y)))
                            Warn($"{where}/{file}: character '{map.CharAt(x, y)}' at {x},{y} is not in the font");
            }
            foreach (MapVariant v in info.Variants) Cond(v.If, $"{where} variant '{v.File}'");

            foreach (TileInfo t in info.Tiles)
            {
                if (t.Char.Length != 1) Error($"{where}: tile char '{t.Char}' must be one character");
                if (t.Interact is not null) { Key(t.Interact, $"{where} tile '{t.Char}'"); Textbox(t.Textbox ?? info.Textbox, $"{where} tile '{t.Char}'"); }
            }

            if (info.Spawn is { Length: 2 } spawn && baseMap.IsSolid(spawn[0], spawn[1]))
                Error($"{where}: spawn {spawn[0]},{spawn[1]} is solid");

            foreach (ExitInfo exit in info.Exits)
            {
                string w = $"{where} exit {exit.X},{exit.Y}";
                Cond(exit.If, w);
                if (baseMap.IsSolid(exit.X, exit.Y)) Error($"{w}: exit tile is solid");
                if (!GameServices.Maps.AvailableMaps.Contains(exit.Map)) { Error($"{w}: unknown map '{exit.Map}'"); continue; }
                MapData target = GameServices.Maps.LoadFile(exit.Map, exit.Map, GameServices.Maps.Info(exit.Map));
                if (target.IsSolid(exit.ToX, exit.ToY)) Error($"{w}: destination {exit.Map} {exit.ToX},{exit.ToY} is solid");
                if (GameServices.Maps.Info(exit.Map).Exits.Any(e => e.X == exit.ToX && e.Y == exit.ToY))
                    Error($"{w}: destination {exit.Map} {exit.ToX},{exit.ToY} is itself an exit (infinite loop)");
            }

            foreach (NpcInfo npc in info.Npcs)
            {
                string w = $"{where} npc '{npc.Id}'";
                if (!baseMap.InBounds(npc.X, npc.Y)) Error($"{w}: out of bounds");
                Cond(npc.If, w);
                foreach (NpcLook look in npc.Variants.Prepend<NpcLook>(npc))
                {
                    if (look is NpcVariant v) Cond(v.If, w);
                    if (!string.IsNullOrEmpty(look.Dialog) && !GameServices.Conversations.All.ContainsKey(look.Dialog))
                        Error($"{w}: unknown conversation '{look.Dialog}'");
                    if (!string.IsNullOrEmpty(look.Interact)) { Key(look.Interact, w); Textbox(look.Textbox ?? npc.Textbox ?? info.Textbox, w); }
                }
            }
            if (baseMap.Width > section.X || baseMap.Height > section.Y)
                if (baseMap.Width % section.X != 0 && baseMap.Width > section.X) { /* partial sections are allowed */ }
        }
    }

    // ---- Conversations -------------------------------------------------------------------

    private static void Assignments(Dictionary<string, System.Text.Json.JsonElement>? set, Dictionary<string, int>? add, string where)
    {
        if (set is not null)
            foreach (var (name, json) in set)
                if (Variable(name, where, write: true) is { } def && !Value.TryFromJson(json, def.Type, out _))
                    Error($"{where}: {name} needs a {Value.TypeName(def.Type)}, got {json.GetRawText()}");
        if (add is not null)
            foreach (string name in add.Keys)
                if (Variable(name, where, write: true) is { } def && def.Type != VarType.Int)
                    Error($"{where}: cannot add to {name}, it is {Value.TypeName(def.Type)}");
    }

    private static void CheckConversations()
    {
        foreach (var (id, conv) in GameServices.Conversations.All)
        {
            string where = $"conversation '{id}'";
            Key(conv.Name, where);
            if (!Enum.TryParse<DrawMode>(conv.DrawMode, true, out _)) Error($"{where}: unknown draw mode '{conv.DrawMode}'");
            if (!Enum.TryParse<CursorMode>(conv.CursorMode, true, out _)) Error($"{where}: unknown cursor mode '{conv.CursorMode}'");
            if (!conv.Nodes.ContainsKey(conv.Start)) Error($"{where}: start node '{conv.Start}' does not exist");

            foreach (var (nodeId, node) in conv.Nodes)
            {
                string w = $"{where} node '{nodeId}'";
                void Next(string? next) { if (next is not null && !conv.Nodes.ContainsKey(next)) Error($"{w}: node '{next}' does not exist"); }
                void Frame(int? frame)
                {
                    if (frame is { } f && !GameServices.Images.Exists(ImageService.CharacterKey(conv.Character, f)))
                        Error($"{w}: image characters/{conv.Character}/{f}.txt does not exist");
                }

                Key(node.Text, w);
                Next(node.Next);
                Frame(node.Image);
                Frame(node.ImageAfter?.Image);
                Assignments(node.Set, node.Add, w);
                foreach (Branch b in node.Branch ?? []) { Cond(b.If, w); Next(b.Next); }
                foreach (DialogOption o in node.Options ?? [])
                {
                    Key(o.Text, w);
                    Next(o.Next);
                    Cond(o.If, w);
                    Cond(o.LockedIf, w);
                    Assignments(o.Set, o.Add, $"{w} option '{o.Text}'");
                }
            }
        }
    }

    // ---- Events --------------------------------------------------------------------------

    private static void CheckEvents(SceneStack stack)
    {
        var ids = new HashSet<string>();
        foreach (EventDefinition e in GameServices.Events.Definitions)
        {
            string where = $"event '{e.Id}'";
            if (!ids.Add(e.Id)) Error($"{where}: duplicated id");
            Cond(e.If, where);
            EventTrigger t = e.Trigger;
            if (!TriggerTypes.All.Contains(t.Type)) Error($"{where}: unknown trigger '{t.Type}'");
            if (t.Type is TriggerTypes.EnterMap or TriggerTypes.Halfway or TriggerTypes.Step)
            {
                if (t.Map is null) Error($"{where}: trigger needs a map");
                else if (!GameServices.Maps.AvailableMaps.Contains(t.Map)) Error($"{where}: unknown map '{t.Map}'");
            }
            if (t.Type is TriggerTypes.Input or TriggerTypes.Change && t.Block is not null)
            {
                if (stack.FindBlock(t.Block) is not { } block) Error($"{where}: there is no block '{t.Block}'");
                else if (!block.RaiseEvents) Error($"{where}: block '{t.Block}' does not have \"raiseEvents\": true");
            }
            if (t.Type == TriggerTypes.Input && t.Action is not null && !InputMap.Definitions.ContainsKey(t.Action))
                Error($"{where}: unknown input action '{t.Action}'");
            if (t.Type == TriggerTypes.Immediate && !e.Once) Warn($"{where}: immediate events always run once");
            Actions(e.Actions, where, stack, null);
        }
        if (!stack.Scenes.Values.SelectMany(s => s.Blocks).Any(b => b.RaiseEvents) && GameServices.Events.Definitions.Count > 0)
            Warn("no block has \"raiseEvents\": true, so no event can ever run");
    }

    // ---- Scenes --------------------------------------------------------------------------

    private static void CheckScenes(SceneStack stack)
    {
        Point grid = Display.Grid;
        foreach (Scene scene in stack.Scenes.Values)
        {
            string where = $"scene '{scene.Id}'";
            SceneDefinition d = scene.Definition;
            if (d.Inputs is not null)
                foreach (var (action, list) in d.Inputs)
                {
                    if (!InputMap.Definitions.ContainsKey(action)) Error($"{where}: input handler for unknown action '{action}'");
                    Actions(list, $"{where} input '{action}'", stack, scene);
                }
            Actions(d.OnEnter, $"{where} onEnter", stack, scene);
            Actions(d.OnResume, $"{where} onResume", stack, scene);
            Actions(d.OnLeave, $"{where} onLeave", stack, scene);
            if (scene.Blocks.Count(b => b is MapBlock { Player: true }) > 1)
                Error($"{where}: only one map block per scene can have \"player\": true");

            foreach (Block block in scene.Blocks) CheckBlock(block, stack, scene);

            // Layout at the minimum grid: what does not fit there is cut on small screens.
            try
            {
                foreach (Block b in scene.Blocks)
                {
                    if (b is BranchBlock branch) branch.UpdateCases();
                    b.RefreshShownForValidation();
                }
                Point size = d.Root.Measure(grid);
                if (size.X > grid.X || size.Y > grid.Y)
                    Warn($"{where}: needs {size.X}x{size.Y} cells but the minimum grid is {grid.X}x{grid.Y}; part of it would be cut");
            }
            catch (Exception e) when (e is ContentException) { Error($"{where}: {e.Message}"); }
        }
    }

    private static void CheckBlock(Block block, SceneStack stack, Scene scene)
    {
        string w = block.Where;
        if (block.VisibleIf is not null) Cond(block.VisibleIf, w);
        if (block.EnabledIf is not null) Cond(block.EnabledIf, w);
        if (block.Style is not null && !Config.Theme.Current.Has(block.Style) && !ColorParser.TryParse(block.Style, out _))
            Error($"{w}: '{block.Style}' is neither a theme colour nor a colour");
        if (block.Inputs is not null)
            foreach (var (action, list) in block.Inputs)
            {
                if (!InputMap.Definitions.ContainsKey(action)) Error($"{w}: input handler for unknown action '{action}'");
                Actions(list, $"{w} input '{action}'", stack, scene);
            }

        switch (block)
        {
            case ContainerBlock c: Key(c.Title, w); break;
            case TextBlock t:
                Key(t.Text, w);
                if (t.TextVar is not null) Variable(t.TextVar, w);
                break;
            case TextBoxBlock tb: Key(tb.Text, w); break;
            case ImageBlock img:
                if (img.Image is { Length: > 0 } key && !key.StartsWith('$') && !GameServices.Images.Exists(key)) Error($"{w}: image '{key}' does not exist");
                break;
            case MapBlock map:
                Key(map.Hint, w);
                if (map.Map is { } id && !id.StartsWith('$') && !GameServices.Maps.AvailableMaps.Contains(id)) Error($"{w}: unknown map '{id}'");
                break;
            case ConversationBlock conv: Actions(conv.OnEnd, $"{w} onEnd", stack, scene); break;
            case CardBlock card: Actions(card.OnEnd, $"{w} onEnd", stack, scene); break;
            case ChoicesBlock ch:
                foreach (ChoiceItem item in ch.Items ?? [])
                {
                    Key(item.Text, w);
                    Cond(item.If, w);
                    Cond(item.LockedIf, w);
                    Actions(item.Actions, $"{w} option '{item.Text}'", stack, scene);
                }
                break;
            case BarBlock bar:
                Variable(bar.Var, w);
                Key(bar.Label, w);
                Expr(bar.Max, w, VarType.Int);
                Expr(bar.Min, w, VarType.Int);
                break;
            case EffectBlock fx:
                Expr(fx.Intensity, w, VarType.Int);
                Actions(fx.OnDone, $"{w} onDone", stack, scene);
                break;
            case TimerBlock timer:
                Cond(timer.When, w);
                Actions(timer.Actions, w, stack, scene);
                break;
            case ButtonControl button:
                Key(button.Label, w);
                Actions(button.Actions, w, stack, scene);
                break;
            case KeyBindControl kb: Key(kb.Label, w); break;
            case ValueControl vc:
                Key(vc.Label, w);
                Variable(vc.Var, w, write: true);
                Actions(vc.OnChange, $"{w} onChange", stack, scene);
                if (vc is SwitchControl sw) { Key(sw.OnText, w); Key(sw.OffText, w); }
                if (vc is ComboBoxControl combo)
                {
                    foreach (ComboOption o in combo.Options ?? []) Key(o.Label, w);
                    if (combo.Options is null && combo.LabelPrefix is { } prefix && VariableRegistry.Find(vc.Var)?.AllowedValues is { } values)
                        foreach (string v in values) Key(prefix + v, w);
                }
                break;
        }
    }

    // ---- Variables and metrics -----------------------------------------------------------

    private static void CheckUnusedVariables()
    {
        foreach (VariableDefinition def in VariableRegistry.Definitions)
            if (def.Scope != VarScope.System && def.Bind is null && !UsedVariables.Contains(def.Name))
                Warn($"variable {def.Name} is declared but never used; remove it");
    }

    private static void WriteTextMetrics()
    {
        TextMetrics metrics = TextMetrics.Compute(GameServices.Loc);
        string source = Paths.SourceAssets();
        string hash = TextMetrics.SourceHash(source);
        foreach (string assets in new[] { source, Paths.Assets }.Distinct())
        {
            string file = Path.Combine(assets, "data", "generated", "text_metrics.csv");
            if (TextMetrics.StoredHash(file) == hash) continue;
            try
            {
                metrics.Write(file, hash);
                if (assets == source) System.Console.WriteLine($"Text metrics regenerated: {Path.GetRelativePath(Environment.CurrentDirectory, file)}");
            }
            catch (Exception e) { Warn($"could not write {file}: {e.Message}"); }
        }
    }
}
