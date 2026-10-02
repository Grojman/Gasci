# Engine redesign v3: scenes from blocks, typed variables, expressions, input actions

Status: **implemented in Gasci 1.0.0.** The [content reference](../reference/) is the reference for authors. This document
records how the design was reached:

- **§R2** has the decisions from the review of v2 and the decisions taken while implementing. Where
  §R2 and the older sections disagree, §R2 wins.
- **§0–§12** are the v2 design, kept for the reasoning behind it.

Compatibility with existing content and saves is **not** a goal (the project is new). When a
system is replaced, the old one is deleted, and the content in `assets/` is migrated in the same
change.

---

## R2. Review of v2, and implementation decisions

### R2.1 Answers to the v2 questions

| Question | Decision |
|---|---|
| Q1 runtime writes out of range | Clamp ints and reject invalid strings, with a logged warning. Resetting to the default is only used for values loaded from files. |
| Q2 prefix | Mandatory everywhere (`game.courage`). |
| Q3 pause/settings | Separate scenes. In the demo, pause is `fill` (not modal); settings and controls are modal. |
| Q4 one player map | One player map for now. Several controllable maps (a minigame) is a future implementation ([roadmap](../roadmap.md)). |
| Q5 autosave | Offered. It can be enabled or disabled, and the frequency is configurable: settings variables bound to `save.autosave` and `save.autosaveMinutes`. |
| Q6 re-layout | Only when the window mode changes. The window cannot be resized by hand. |
| Q7 rebinding a used key | Swap. |
| Q8 cancelling a rebind | Enter cancels; Enter is the only non-bindable key. |

### R2.2 Decisions of this round

1. **Events are indexed by trigger.** The review offered two options: store events by what triggers them,
   or make the condition a fixed value recalculated only after an event runs. I implemented the first,
   because the second gives stale results: a variable written by a conversation would not be seen until
   some event ran. As a result:
   - Triggered events (`enterMap`, `step`, `halfway`, `input`, `change`) live in a dictionary keyed by
     trigger and map/block. A trigger only looks at its own list and checks the condition at that moment.
     Writing a variable re-evaluates nothing.
   - `immediate` events have no trigger. A dependency index (variable → immediate events) marks the
     affected ones dirty, and they are checked once per frame. Events whose condition reads `system.*`
     are checked every frame, because those values change without a write.
   - A ready event re-checks its condition right before it runs (another event may have changed things).
2. **`raiseEvents`** is a property of every block. A block with it:
   - notifies the engine of the input it receives (`input` trigger) and of its changes (`change` trigger);
   - lets events run while it is focused and idle.

   The map has it in the demo, so the game behaves as before. An inventory menu with `raiseEvents`
   lets events run without the map on screen.
3. **The map has no dialog box.** Interaction texts go to the textbox named by `textbox` (on the map info,
   or on a tile/NPC). `message` actions must name their `target`. A missing textbox is an error
   (`--validate`, and at runtime). So is a textbox in a scene that is not on top, since its text could
   never be read.
4. **Block ids are unique in the whole game.** For every block to be reachable at any moment, every scene
   is created once at start-up and lives for the whole game. Two consequences:
   - A scene can be only once in the stack.
   - Opening a scene again re-applies its parameters, and the blocks reset in `OnOpen`. Children reset
     before their parents, so composite blocks drive their slots after the slots reset.
5. **Save = exact restore.** The save stores:
   - the stack of scenes marked `"saved": true`, with their parameters;
   - the state of their blocks (the map stores its map, position and facing);
   - game variables, completed events and ready events.

   Menus such as pause are not saved scenes, so saving from the pause menu restores into the game.
6. **Music is independent of who calls it.** Maps keep playing their music on load and scenes can set one.
   Stopping it is explicit (a `music` action with `""` or a scene with `"music": ""`).
7. **Validation may be slow.** `--validate` measures every scene at the minimum grid, resolves every
   reference, and regenerates the text metrics.
8. **Variable mismatches are always logged.** Controls also block illegal values on screen:
   - sliders stop at their limits;
   - number fields refuse digits that would leave the range;
   - text fields refuse characters past `maxLength`, play the "locked" sound and show a counter.
9. **Keybind controls show one column per key slot** (primary, secondary...), as many as `input.maxKeys`.
10. **Custom actions** are declared in the global `actions.json`; blocks and scenes define what they do in `inputs`.
11. **Window modes:** windowed, fullscreen and borderless, chosen in the settings. The window is not resizable.
12. **Custom fonts** need a character map, so the engine does not rely on the IBM layout. They are checked
   by `--validate` and again at start-up. A TTF converter is a future, separate tool ([roadmap](../roadmap.md)).
13. **Log:** each session writes warnings and errors to `log.txt` in the user folder. The previous session
   is kept as `log.previous.txt`, and nothing is written when nothing went wrong.

### R2.3 Where the implementation differs from §0–§12

| Topic | Design said | Implemented | Why |
|---|---|---|---|
| Percentage rounding (§3.3) | `round()` of the cumulative share | `floor()` of the cumulative share (the fraction is carried to the next element); the last element absorbs the rest when the shares add up to 100 % | This is the rule asked for in the review ("pass the remaining size onto the next element"). With round(), 3 × 33 % of 100 gave 33/34/33. |
| Grid in fullscreen (§3.10) | Biggest integer font scale whose grid reaches the minimum, extra cells beyond it | The minimum grid is scaled to fill the screen on its tighter axis; the other axis gets extra cells. The font scale is the biggest that does not shrink below 50 %. | With integer scales only, a 1920×1080 screen dropped to scale 1 (a 240×67 grid of tiny 8×16 characters). Now it gets 128×36 cells at the designed size. |
| Windowed size (§3.10) | Biggest scale fitting 90 % of the screen | The minimum grid rendered at the biggest scale and scaled to 90 % of the screen | Keeps the previous look (a big window on any screen). |
| Text metrics location (§3.4) | `assets/data/generated/text_metrics.csv` | Same, written to the source `assets/` (found by walking up from the executable) and to the build copy | — |
| Image minimum character (§3.8) | Pixels | Pixels **at font scale 1** (`[1.5, 3]` = 3×6 at scale 2) | The font scale now changes at runtime. |
| `ImageDetail` | Global constant | `images.detail` in game.json, and `detail` per image block | — |
| Map distortion glitch | Hard-coded in the map | An `effect: glitch` block over the map, with `intensity: "game.distortion"` | The map must not know about game variables. |
| Black screen (§3.7) | Image + text + timer blocks | A `card` composite block with `image` / `text` / `soloText` slots | The order "image, then text, then wait or confirm" is logic, not layout. |
| New blocks | — | `choices` (dialog answers, standalone prompts), `timer`, `effect`, `bar`, `spacer`, `card` | Needed to rebuild every existing screen as data. |
| New text code | — | `{k:action}` prints the key bound to an action | The hints ("E interact") would be wrong after rebinding. |
| Image skip (§9) | `ui.confirm`/`ui.back` | Same, handled by the scene for every shown skippable image before the focused blocks | Images are rarely focused (the title menu is). |
| Validation of expressions | All errors listed | Errors in the files the game loads first (variables, events, scene conditions) stop the load with the first error; everything else is listed | Those files are compiled while loading. |

### R2.4 Not verified yet

The game was **not launched** during this work (only built, unit-tested and validated). These parts depend on
SadConsole/MonoGame at runtime and need a manual pass:

1. Switching display modes (windowed ↔ fullscreen ↔ borderless) and the re-layout that follows.
2. The look of every rebuilt screen compared with the old C# scenes: title, pause, settings, controls,
   map, dialog, black card and exit.
3. Rebinding keys on the controls page, including swapping and cancelling with Enter.
4. Save → continue (scene stack, player position, events) and autosave.
5. A custom font (none is shipped; the check is covered by the validator only).

---

## 0. Decisions from the review of v1

| v1 item | Decision |
|---|---|
| §2.2 presentation model | Kept: layout (`fill` / `anchored` / `custom`) is separate from modality. |
| §2.3.1 "Modal" | Not a layout. `modal` is only a **preset** that expands to "anchored centre, drawn over the scene below, dimmed backdrop". |
| §2.3.2 fit to content | Yes. Every block measures the minimum size it needs before it is shown. |
| §2.3.3 text sizes | Precomputed by `--validate`, stored in a separate generated file keyed by text key, read by a `TextMetrics` service (§3.4). |
| §2.3.4 percentage rounding | The rounding remainder is carried to the next element; the last element absorbs what is left (§3.3). |
| §2.3.5 percent of what | Of the **parent's** available size. Of the screen only for the scene's root. |
| §2.3.6 positions in cells | Yes (`PositionFontSize`). Authors never deal with cells or pixels, only with "how much of the screen". |
| §2.3.7 / §2.3.8 stack updates and overlays | Solved by the new scene model: scenes are built from **blocks**, only **one scene is enabled** at a time, and focus decides which block gets input (§3). |
| §2.3.9 images | Images get CSS-like fit modes (`contain`, `cover`, `fill`, `none`, `scale-down`) plus a position. Image files no longer care how they are shown (§3.8). |
| §2.4 window size | The grid is computed from the real window/screen size, and only recomputed when the window mode changes. Game width and height become editable data (§3.10). |
| §3 variables | One **template** file per scope (type, default, limits) plus one **values** file per persistent scope. Values out of the template's rules reset to the default. Scopes are chosen by a prefix (§4). |
| §3.3 | Rebuild the system and delete what is unused; a missing default is a load error; values live in memory and are written to disk with a debounce timer; old saves need not load. |
| §4 conditions | Become a typed **expression** language: arithmetic, comparisons, logic, strings, a few functions (§5). |
| §5 menus | Kept, inside the block model. Buttons run a **list** of actions (several variables, sounds...) (§6). |
| §7 limitations | No real drop-down: keep the `◄ value ►` cycling. **No mouse**, the engine is keyboard-only. Look into custom fonts (§3.11). |
| Q4 branches | A `mode` property chooses `first` (default) or `all`. |
| Q6 combo box | Keep cycling; when the values file has no value yet, use the template's default. |
| Q7 undeclared variables | Throw: the game refuses to start, and `--validate` reports each one. |
| New | Input is no longer hard-coded: keys map to **actions** declared in a file, the player can rebind them, and every action has a default to restore (§7). |

---

## 1. Findings from the code (still valid, plus new ones)

v1 §1 still describes the current code. Additional findings that this version depends on:

- **Saves depend on the section size.** `SaveGame` stores `SectionX/Y` and `LocalX/Y`, and
  `MapScene.LoadSection` multiplies by `GameConfig.MapViewWidth/Height` (= screen − 2). As soon as
  the grid size depends on the window (§3.10), a save made on one monitor would load at a different
  position on another. `halfway` triggers (`section: 1`) have the same dependency.
- **Hard-coded cell positions** exist in every scene: `MenuY = 21`, the pause box `(30,7,40,21)`, the
  map message box `(8, Height-9)`, `DialogTextHeight`, `BlackImageTop`. All of them become layout data.
- **`UserSettings` side effects** are written in C# next to the menu that changes them
  (`Audio.ApplyVolumes`, `Loc.Language`, `Display.SetFullScreen`).
- **Event actions and menu actions are two models of the same thing.** Events have
  `EventAction` (set, add, sfx, music, dialog...). v1 proposed a second, separate action model for
  buttons. v2 merges them (§6.3).
- **`InfoEngine.Set` re-evaluates every event on every write**, by scanning all definitions.
- **SadConsole caches each surface in a texture** and only re-renders a surface when it is dirty.
  Composition of the surfaces is a GPU blit per surface, not per cell. This matters for the rendering
  analysis in §3.9.
- **SadConsole 10.10.1 has what §3.10 and §3.11 need:** `Game.WindowResized` (event),
  `FontConfig.UseCustomFont(path)` / `AddExtraFonts(...)` / `GameHost.LoadFont(path)`, and
  `Settings.Input.DoMouse` to switch off mouse processing.
- **There is no test project.** The expression parser, the layout engine and the variable rules are
  pure logic and should get unit tests (`tests/Relato.Tests`, xUnit).

---

## 2. Overview of the new architecture

```
game.json ──► GameDefinition (grid, font, start scene, new-game setup, theme)
                    │
variables/*.json ──►│ VariableRegistry ◄── values: save.json / settings.json (debounced writes)
                    │      ▲
                    │      │ compiled, typed expressions (conditions, action values, text {v:})
input/actions.json ►│ InputMap ◄── keybindings.json (player overrides)
                    │
scenes/*.json ─────►│ SceneStack ── only the top scene is enabled; modal scenes draw over the one below
                    │    └─ Scene ── root block tree ── layout (measure / arrange) ── focus manager
                    │          └─ blocks: containers, text, textbox, image, map, conversation, controls...
                    │
events.json ───────►│ EventEngine ── runs action lists through the shared ActionRunner
```

One action model (§6.3) is shared by events, buttons, control changes, scene input handlers and
scene enter/leave hooks.

---

## 3. Part A — Scenes, blocks and layout

### 3.1 Scenes

Every scene, including the ones that are C# classes today (title, settings, pause, game, dialog,
black, exit), becomes **the same `Scene` class** loaded from `assets/data/scenes/<id>.json`. What
changes between scenes is only their content: a tree of **blocks**.

```jsonc
// scenes/game.json — the map, its message box and an optional HUD
{
  "params": { "map": "string", "x": "int", "y": "int" },    // given by whoever opens the scene
  "presentation": "fill",                                   // or "modal", or the explicit form below
  "music": null,                                            // null = keep; "" = stop; "id" = play
  "inputs": {                                               // scene-level handlers (§3.6)
    "pause": [ { "type": "open", "scene": "pause" } ]
  },
  "root": {
    "type": "overlay",
    "children": [
      { "type": "map", "id": "world", "player": true, "focus": true,
        "map": "$map", "x": "$x", "y": "$y", "messages": "msg" },
      { "type": "textbox", "id": "msg", "visible": false,
        "layout": { "type": "anchored", "x": "center", "y": "bottom", "width": "84%", "height": "auto" } },
      { "type": "bar", "var": "game.health", "max": "game.max_health",
        "visibleIf": "game.has_health_bar",
        "layout": { "type": "anchored", "x": "left", "y": "top", "width": "20%" } }
    ]
  }
}
```

**The scene stack stays, but only the top scene is enabled.**

- `open` pushes a scene; `back` pops it; `goto` replaces it; `closeAll` returns to the bottom scene
  of the current "base" (game or title); `replaceAll` is used by new game / continue / return to
  title.
- The top scene is the **only one updated and the only one receiving input**. The scenes below are
  frozen (`IsEnabled = false`): the map stops gliding and animating under the pause menu, and timed
  effects do not progress behind a menu. This removes v1 problem 7.
- A scene below is **drawn** only if the scene above lets it show through (§3.2). An opaque `fill`
  scene hides everything below (`IsVisible = false` below it), which is also the cheapest case.
- **Events run** only when the top scene contains the player's map block, that block holds focus,
  and it is idle (no step in progress, no message open). This replaces `Top == Map` and removes v1
  problem 8: a HUD is a block *inside* the game scene, so it never makes the map stop being on top.
- Scene instances **live while they are in the stack**. Opening the dialog scene over the game keeps
  the game scene (and its map state) alive below it. Returning pops the dialog and re-enables the
  game.

**Scene parameters.** Several scenes are templates that need data from whoever opens them: the
dialog scene needs a conversation, the black scene needs a text key and an image, the game scene
needs a map and a position. A scene declares typed `params`; any block property can reference one
with `"$name"`. `open` / `goto` and event actions pass them:
`{ "type": "open", "scene": "dialog", "params": { "conversation": "mother_morning" }, "wait": true }`.
The validator checks that every `$name` is declared and that every opener passes all of them with
the right type.

### 3.2 Presentation and the `modal` preset

```jsonc
"presentation": {
  "layout": { "type": "anchored", "x": "center", "y": "center" },  // fill | anchored | custom
  "backdrop": "dim"                                               // none | dim | opaque
}
```

- `backdrop` decides how the scene below is shown: `none` (drawn as it is), `dim` (drawn under a
  translucent layer) or `opaque` (not drawn). A `fill` scene with an opaque background behaves like
  `opaque`.
- `"presentation": "modal"` is shorthand for
  `{ "layout": { "type": "anchored", "x": "center", "y": "center" }, "backdrop": "dim" }`.
  `ScenePresentation.Modal` is the matching static value in C#. Modal is not a fourth layout and
  has no behaviour of its own: since only the top scene is enabled, every scene above another one
  already blocks input and freezes what is below.
- `"presentation": "fill"` is shorthand for the default.
- Pause and settings stay **separate scenes** opened with the modal preset. They are reachable from
  both the title and the game, so making them blocks of the game scene would duplicate them.

### 3.3 Layout

**Units.** The author writes sizes as:

| Value | Meaning |
|---|---|
| `"auto"` (default) | The block's measured minimum size (fit to content). |
| `"40%"` | 40 % of the parent's available size (of the screen for the scene root). |
| `12` | 12 cells. Allowed, but discouraged: the grid size depends on the screen (§3.10). |

`minWidth` / `maxWidth` / `minHeight` / `maxHeight` take the same values. Positions are always in
cells internally (`Scene` sets `PositionFontSize = font cell size`); authors never see pixels.

**Two passes**, as in v1: `Measure(available)` bottom-up returns the minimum size, `Arrange(rect)`
top-down places each block.

**Containers**:

- `stack`: `direction` (`vertical` / `horizontal`), `spacing`, `align`. Children flow along the
  main axis; a child's `x`/`y` anchor only applies on the cross axis.
- `overlay`: children are placed by their own anchors and may overlap. Later children are drawn on
  top (z-order = document order).
- Both have `border`, `padding`, `background`, `title`.

**Percentages in a stack.** The available size is the parent's inner size (after border, padding and
spacing). Fixed and `auto` children are measured first; percentage children take their share of the
available size.

- **Rounding.** The remainder is carried forward: child *i* starts at
  `round(total × (p₀+…+pᵢ₋₁) / 100)` and ends where child *i+1* starts. This is the "pass the
  leftover to the next element" rule; the last child absorbs whatever is left. Three 33 % children
  of 36 rows get 12, 12, 12; of 100 columns 33, 33, 34.
- **Sum above 100 %** in one container is a validation error.
- **Not enough room** (fixed + `auto` + percentage children larger than the parent, which can now
  happen on small screens): percentage children shrink proportionally first, never below their
  `min*`. If it still does not fit, the overflowing children are clipped and the game logs a
  warning with the scene and block id.
- **Sum below 100 %**: the free space is placed according to the container's `align`.

### 3.4 Text metrics

The size a text needs depends on the text and on the font, **not** on runtime state (except for the
cases listed below), so it can be computed ahead of time.

**What is stored**, per text key and language, in **characters** (every SadConsole font is
monospaced, so one character is one cell at scale 1):

| Column | Meaning |
|---|---|
| `natural` | Width of the longest line when nothing is wrapped (style codes like `{c:Red}` removed). |
| `min` | Width of the longest word: the narrowest width the text can wrap to. |
| `lines` | Number of lines when nothing is wrapped (`\n` count + 1). |

Plus, per key, the maximum of each column across all languages. Blocks measure with the maximum
across languages so that **changing the language does not resize the menu** (v1 §2.3.3).

At runtime, a text block of scale *s* needs at least `min × s` columns, prefers `natural × s`, and
its height for a given width is computed by the same `TextLayout.Wrap` the `TextBox` uses (cheap,
cached per width). Wrapped height cannot be precomputed because the width is only known at layout
time.

**Where it is stored.** Not as a column of `dialogs.csv`: that file is edited by hand (and by
translators), so a generated column would go stale the first time someone edits a text without
running the validator, and it mixes authored and generated data. Instead:

- `--validate` writes `assets/data/generated/text_metrics.csv` (`key, language, natural, min, lines`)
  and a header line with the SHA-256 of `dialogs.csv`.
- `TextMetrics` (new service) loads it and answers `Measure(key)`.
- If the hash does not match the current `dialogs.csv`, the game computes the metrics in memory at
  start-up (a few milliseconds for thousands of texts) and logs a warning. `--validate` fails when
  the file is stale, so it can be checked before a release.

**Texts whose size does depend on runtime state:**

- Texts with a **variable code** `{v:game.player_name}` (new inline code, §6.2). The metric uses the
  variable's declared limit: `maxLength` for strings, the widest of `min`/`max` for ints. The
  validator rejects `{v:}` on a variable with no limit.
- Texts shown through `textVar` (the key is in a variable). The block measures the widest of the
  variable's `options` when it declares them; otherwise it needs an explicit width.

### 3.5 Block catalogue

All blocks share `id`, `layout`, `visibleIf`, `enabledIf`, `style`, `focus` (initially focused),
`backdrop` (draw a dimmed/opaque layer under the block, for popups inside a scene) and `inputs`
(action handlers, §3.6).

| Block | Purpose | Replaces |
|---|---|---|
| `stack`, `overlay` | Containers (§3.3). | Hand positioning |
| `text` | Static or variable text, optional `scale` (big text), `wrap`, alignment. | `Frame.Print*` |
| `textbox` | Typewriter text with pages and the ▼/► indicator. Shows a queue of keys; `advance` on `ui.confirm`. Used for map messages and dialog lines. | `TextBox` usage in `MapScene`, `DialogScene`, `BlackScene` |
| `image` | ASCII image with fit modes (§3.8), draw animation (`drawMode`, `cursorMode`, `duration` or `speed`), `color`, `skippable`. `frames` + `frameSeconds` for simple animations. | `ImageCanvas`/`DrawCursor` usage |
| `map` | A map viewport: map id, start position, `player` (this map owns the player), `messages` (id of the textbox used for messages). | `MapScene` |
| `conversation` | Drives a conversation: owns three slots, `portrait` (image), `text` (textbox) and `options` (choices), each laid out like any block. | `DialogScene` |
| `choices` | A vertical list of options with locked state and countdown. Used by `conversation`, also usable alone (yes/no prompts). | `DialogScene.DrawOptions` |
| `menu` | A `stack` whose children are controls, with focus navigation (§6.1). | `Menu` |
| controls | `button`, `switch`, `slider`, `combobox`, `numberfield`, `textfield`, `keybind` (§6.1, §7). | `SettingsScene` items |
| `branch` | `cases: [{ if, content }]`, `else`, `mode: "first" | "all"`. | — |
| `bar` | Read-only gauge bound to an int variable (`max` is a number or an expression). Health, progress. | — |
| `effect` | Full-area procedural effect: `dissolve` (the exit "black bits"), `glitch` (the map distortion), `fade`. Parameters may be expressions (`"intensity": "game.distortion"`). | `ExitScene`, `MapScene.DrawDistortion` |
| `timer` | Invisible: runs an action list after N seconds, or when a condition becomes true. Needed for "close after `seconds`" in the black screen without C#. | `BlackScene` timing |
| `spacer` | Empty space of a given size. | — |

Considered and **left out for now**:

- `list` / `grid` repeated over a collection (inventories). It needs a collection variable type
  (arrays), which §4 does not add. It is the natural next step after this redesign.
- A message `log` / backlog block. Easy to add later on top of `textbox`.

### 3.6 Focus and input routing

Input arrives as **actions** (§7), never as raw keys, except in a text field or a keybind control
that is capturing.

- A scene has a **focus set**: the blocks that receive input. Initially, the blocks with
  `"focus": true`. A container with focus passes it to its focused child (a `menu` to its selected
  control).
- **Dispatch.** For each action triggered this frame, the focused blocks are visited in **z-order,
  top first**. A block that handles the action **consumes** it, and lower blocks do not get it.
  If no block consumes it, the scene's `inputs` handlers run (for example `pause`). A block can set
  `"passInput": true` to react without consuming.
- **Focus changes** are an action, `{ "type": "focus", "target": "<block id>", "mode": ... }`, run
  by buttons, events, block hooks, or the engine itself:

| `mode` | Origin (the block running the action) | Target | Example |
|---|---|---|---|
| `move` (default) | loses focus | gains focus | The map shows a message: focus moves to the textbox; the map stops walking. |
| `share` | keeps focus | gains focus | A HUD hotbar listens to `hotbar.*` actions while the map keeps walking. |
| `release` | loses focus | — | The message box closes. |

- **Focus history.** Each `move` remembers the origin on a stack. When a `release` leaves the scene
  with no focused block, focus returns to the last origin on the stack (the message closes → the
  map gets focus back). If the stack is empty, the blocks marked `"focus": true` get it back.
  A scene with no focused block and no history is a validation error.
- **Hidden or disabled blocks** lose focus as if they ran `release`.
- **Remembered selection.** Each scene remembers the selected control of each `menu` across
  `OnResume`, so returning from settings keeps "Settings" selected in the pause menu.
- **Shared actions between focused blocks.** If the map and a shared hotbar both handle
  `ui.confirm`, the topmost consumes it. The validator warns when two blocks that can be focused at
  the same time handle the same action without `passInput`.

### 3.7 The current scenes rebuilt as data

| Today | v2 |
|---|---|
| `TitleScene` | `scenes/title.json`: `overlay` with an `image` (title art, `drawMode: random`, skippable), a `menu`, a hint `text`. |
| `PauseScene`, `SettingsScene` | `scenes/pause.json`, `scenes/settings.json` with `"presentation": "modal"`. Settings gains a "Controls" button opening `scenes/controls.json` (§7.4). |
| `MapScene` | `scenes/game.json` with a `map` block and a `textbox` for messages. |
| `DialogScene` | `scenes/dialog.json` with a `conversation` block; param `conversation`. |
| `BlackScene` | `scenes/black.json`: `image`, `text` (with `scale`), `timer`; params `key`, `image`, `seconds`, `big`, `drawMode`. |
| `ExitScene` | `scenes/exit.json`: modal, `backdrop: none`, an `effect: dissolve` that runs `quit` when done. |

**Map block specifics.**

- **Section size is game data, not screen size.** `game.json` gets `map.sectionWidth/Height`
  (default 98×34, today's value). The map block shows exactly one section. If the block is larger it
  centres the section; if it is smaller it uses a smaller font for the map surface (as images do,
  §3.8) so the section still fits. Map design and `halfway` triggers stop depending on the window.
- **Saves store the global position** (`x`, `y` on the map) instead of section + local position.
- **Only one map block may have `"player": true`.** It owns the player, fires step/enter events,
  and is the one saved. Other map blocks are views (a second location, a minimap): they show a map,
  optionally follow a variable-driven camera, and never move the player. Two player maps would mean
  two players and two positions in the save, which the event system has no way to address.
- **Messages.** The `message` event action shows text in the textbox named by the player map's
  `messages` property; the textbox takes focus with `move` and releases it when the queue ends.

### 3.8 Images: fit modes

The image file only contains the drawing. How it is shown is the block's job, with the same terms
as CSS `object-fit` / `object-position`:

| `fit` | Behaviour in a character grid |
|---|---|
| `contain` (default) | The biggest image character that shows the whole image keeping its proportions (today's `ImageCanvas.FitCharSize`). Letterboxed. |
| `cover` | The smallest character that fills the whole block keeping proportions; what overflows is cut. |
| `fill` | Characters stretched independently in X and Y to fill the block exactly (the drawing is deformed). |
| `none` | Characters at the default image detail; what does not fit is cut. |
| `scale-down` | `none` if the image fits, otherwise `contain`. |

`position` (`"center"`, `"top left"`, ...) places the image inside the block for `contain`, `none`
and `scale-down`, and chooses which part is cut for `cover` and `none`.

Limits:

- **No down-sampling of the drawing.** An image is shrunk by using smaller characters, never by
  dropping rows or columns of art. Characters have a minimum size (today 3×6 pixels;
  `game.json → images.minCharSize`) below which they are unreadable. An image that would need
  smaller characters is cut in `contain` mode and the game logs a warning. The validator cannot
  check this anymore (the block size depends on the screen), but it checks the image against the
  **minimum** grid from `game.json`.
- Character sizes are whole pixels, so `contain` is only approximately proportional (today's
  `MaxShapeError` tolerance stays).
- `ImageDetail` (one global value today) becomes the `none` mode's detail, configurable per block.

### 3.9 Rendering: the "paint top to bottom and mark pixels" proposal

The proposal: draw from the top layer down, mark each painted cell as drawn, and have the lower
layers skip marked cells, so nothing is painted twice.

**Analysis.** It is the classic front-to-back occlusion optimisation, and it has five problems here:

1. **Transparency.** The `dim` backdrop is translucent, text boxes and image canvases have
   transparent backgrounds, and a glyph only covers part of its cell. A cell drawn by an upper
   layer only hides the lower one when its background is **fully opaque**. The mask must therefore
   mean "opaquely covered", and most of the cells of a menu over the map are not.
2. **Different grids.** Image canvases and big text use other character sizes (3×6, 32×64 pixels...)
   than the normal grid (16×32). A mask needs a common unit; with cells of different sizes, a normal
   cell is covered only when an upper surface covers it *completely*, which is rare at the edges.
3. **Blending order.** Translucent layers must still be blended bottom to top to look right. The
   mask can only skip work, not change the order of composition.
4. **Where the cost actually is.** SadConsole renders each surface into a cached texture **only when
   it is dirty**, then composes the textures on the GPU (one blit per surface). Skipping individual
   cells saves almost nothing in composition. The real costs are our code clearing and rewriting
   whole surfaces every frame, and the image canvases (a full-screen portrait has about 94 000
   small characters).
5. **Incremental drawing.** `DrawCursor` animates by writing only the cells that change, and relies
   on the surface keeping what it wrote. With a mask, uncovering a region (a popup closing) requires
   redrawing the cells of the lower layers that were skipped, so every block would need a way to
   redraw an arbitrary region. That is a large amount of complexity to save very little.

**Decision: do not implement a per-cell mask.** Do these instead, which give the benefit at block
granularity, where the cost is:

- **Block culling.** After arranging, any block (or scene below) whose rectangle is completely
  inside an opaque block above it is set `IsVisible = false` and skips its draw code. This is exact,
  cheap to compute (rectangle containment, recomputed on layout changes only), and it is the common
  case (a `fill` scene over another, a full-screen portrait over the map).
- **Dirty drawing.** Blocks redraw their surface only when their state changed (text typed, value
  changed, map moved), instead of `Clear()` + redraw every frame as the scenes do today. SadConsole
  then re-renders only those surfaces.
- **Frozen scenes below** (§3.1) are not updated and, if not dirty, not re-rendered.

If profiling ever shows a specific block is expensive under a partial cover, a per-block "covered
region" can be added to that block alone.

### 3.10 Grid size from the window, editable game size

`GameConfig.Width/Height` (constants) become **minimum grid** values in `game.json`, editable by the
game author:

```jsonc
// assets/data/game.json
{
  "title": "Relato",
  "grid": { "minWidth": 100, "minHeight": 36, "fontScales": [2, 1] },
  "font": null,                                   // §3.11; null = built-in IBM 8x16
  "map": { "sectionWidth": 98, "sectionHeight": 34 },
  "images": { "minCharSize": [3, 6], "detail": 2 },
  "defaultLanguage": "es",
  "startScene": "title",
  "newGame": { "scene": "game", "params": { "map": "home" } }
}
```

**Algorithm.** At start-up and **only when the window mode changes** (windowed ↔ full screen):

1. Take the pixel size of the target: the monitor in full screen, the window client area in
   windowed mode.
2. Pick the **largest** font scale in `fontScales` for which
   `floor(pixels / cellSize) ≥ (minWidth, minHeight)`. If none qualifies, use the smallest scale and
   the minimum grid, and let `ResizeMode.Fit` shrink it (today's behaviour).
3. Resize the root surfaces to the new grid and re-run layout on **every** scene in the stack.

Between mode changes, resizing the window does not re-layout; `ResizeMode.Fit` scales the current
grid as it does today. (See Q6 for a debounced re-layout on resize as an option.)

**Consequences that must be handled:**

- A 1920×1080 screen at scale 2 gives 120×33, which is **below** the 36-row minimum, so it falls to
  scale 1 (240×67). Layouts written in percentages adapt; layouts in fixed cells look small. This is
  why §3.3 discourages fixed cells.
- **Re-layout mid-scene.** Image canvases are rebuilt: an image still animating is completed
  immediately. A `textbox` re-paginates and keeps the current character position. Map blocks keep
  their state (the section size does not change, §3.7).
- **SadConsole support.** Changing the grid of a running game (`Game.Instance.ResizeWindow` plus
  resizing our root surfaces, or rebuilding the scene surfaces) has to be prototyped: SadConsole is
  built around a fixed starting grid. This is the main technical risk of the redesign, and it is
  isolated in its own phase (§10).

### 3.11 Custom fonts

Possible, with conditions. SadConsole loads **bitmap tile fonts**: a `.font` JSON file plus a PNG
tile sheet (`FontConfig.UseCustomFont(path)`, `GameHost.LoadFont(path)`). `game.json → font` names
one.

- **Bitmap only.** TTF/OTF fonts are not supported by SadConsole. A converter (rasterise a TTF into a
  tile sheet) would be a separate tool; not part of this redesign.
- **Cell size comes from the font**, so `GameConfig.FontCellSize` (16×32) becomes
  `font glyph size × scale`, and the image character proportions follow the font's shape.
- **Glyph mapping.** `Cp437.ToGlyph` assumes the IBM layout. A font with another layout needs a
  character map: `.font` + an optional `charmap` (Unicode → glyph index) in `game.json`. Without it,
  the font must use the CP437 layout. The validator checks that every character used in texts,
  maps and default variable values has a glyph in the selected map.
- **Text metrics** (§3.4) do not change with the font: tile fonts are monospaced, so metrics are in
  characters.

### 3.12 Problems specific to the block model (and their solutions)

1. **Cross-block references** (`messages: "msg"`, focus targets, conversation slots) can break.
   Block ids are unique per scene, and the validator resolves every reference.
2. **Events that targeted a scene class** (`dialog`, `black`, `message`, `teleport`) now target
   scenes and blocks by id. The event actions keep their short forms and expand to the generic ones
   (`dialog` = `open dialog {conversation} wait`); `teleport` and `message` target the player map
   block of the scene stack.
3. **Event waiting.** `dialog` and `black` wait until the scene closes, as today. The generic `open`
   gets `"wait": true`, implemented with the same continuation callback the event engine already
   uses (`Action done`).
4. **New game / continue** now need to know which scene holds the game: `game.json → newGame`.
   Continue rebuilds the same scene with the saved map and position.
5. **The map's music** is played by the map block on load; scene `music` is for menus. A map block
   in a frozen scene does not change the music.
6. **Validation cost.** `--validate` must instantiate and lay out every scene at the minimum grid to
   catch overflows and missing references. Runs without a window (layout is pure logic; surfaces are
   not needed for measuring).

---

## 4. Part B — Variables

### 4.1 Scopes and files

| Scope (prefix) | Template (assets) | Values (user data) | Lifetime |
|---|---|---|---|
| `game.` | `data/variables/game.json` | inside `save.json` | Reset to defaults on New game, written on save, read on continue. |
| `settings.` | `data/variables/settings.json` | `settings.json` | Survive new games and loads. Written with the debounce (§4.4). |
| `session.` | `data/variables/session.json` | none | Reset to defaults at start-up; never stored. |
| `system.` | none (built into the engine) | none | Read-only, computed: `system.save_exists`, `system.can_save`, `system.in_game`, `system.languages`... |

**Names always carry their scope prefix** in conditions, actions, controls and text codes:
`game.courage`, `settings.music_volume`. Inside a template file the names are declared without
the prefix (the file is the scope). Dots after the prefix are allowed for grouping
(`game.stats.strength`).

### 4.2 Template format

```jsonc
// data/variables/game.json
{
  "courage":     { "type": "int",    "default": 0, "min": 0, "max": 10 },
  "distortion":  { "type": "int",    "default": 0, "min": 0 },
  "met_shadow":  { "type": "bool",   "default": false },
  "player_name": { "type": "string", "default": "Ana", "maxLength": 12 }
}

// data/variables/settings.json
{
  "music_volume": { "type": "int",    "default": 6, "min": 0, "max": 10, "bind": "audio.music" },
  "sfx_volume":   { "type": "int",    "default": 7, "min": 0, "max": 10, "bind": "audio.sfx" },
  "language":     { "type": "string", "default": "es", "optionsFrom": "languages", "bind": "loc.language" },
  "fullscreen":   { "type": "bool",   "default": false, "bind": "display.fullscreen" },
  "text_speed":   { "type": "int",    "default": 2, "min": 0, "max": 4 }
}
```

- Types: `int`, `bool`, `string`. (No floats: every current use is integral, and integer
  arithmetic keeps expressions exact.)
- Rules: `min` / `max` (int), `maxLength` (string), `options` (a fixed list of allowed values) or
  `optionsFrom` (an engine list: `languages`).
- **`default` is mandatory.** A missing default, a default of the wrong type, or one that breaks
  its own rules is a load error: the game does not start and `--validate` reports it.
- **`bind`** connects a variable to an engine side effect. The engine has a fixed table of binding
  names (`audio.music`, `audio.sfx`, `loc.language`, `display.fullscreen`), each with a required
  type. When the variable changes, the binding is called. This replaces `UserSettings` and the C#
  code that applies settings. The validator checks binding names and types, and that a binding is
  used only once.

### 4.3 Validation of values

Values are checked in **one place**, the registry's write method.

- **Loading a values file** (save or settings): a value with the wrong type or outside the rules is
  **reset to the default** and a warning is logged. Values whose variable is no longer in the
  template are dropped. Variables missing from the file take their default.
- **Writes at runtime** (events, buttons, controls): see Q1. Recommendation: clamp ints to
  `min`/`max` and reject strings that break `maxLength`/`options` (keeping the old value, with a
  logged warning). Resetting to the default at runtime would turn "the potion overfilled the
  health" into "health dropped to its default", which is a gameplay bug rather than protection.
- **Undeclared variables** throw. `--validate` lists every reference to an undeclared name, with
  where it is used.
- **Unused declared variables** are reported by `--validate` as warnings, so they can be removed.

### 4.4 Memory and persistence

- Values live **in memory** in typed slots; everything reads them from memory. Files are only for
  persistence.
- **Debounced writes.** Every write to a persisted scope marks it dirty and restarts that scope's
  timer (`game.json → persistence.delaySeconds`, default 1 s). When a timer reaches zero, the scope's
  file is written. A slider moved ten times in a row produces one write.
- **No lock needed** in the basic version: the timer is ticked from the game's `Update`, on the main
  thread, and the write is synchronous (the files are a few kilobytes). If writes are later moved to
  a background task, the scope is copied to a snapshot on the main thread and only one write is in
  flight per file; a write requested while one is running is queued once.
- **Flush** pending writes immediately on quit (including closing the window), on return to title,
  and before the exit effect.
- **Game scope is not debounced to disk.** Game values are part of the save, which is written by the
  `save` action only (manual save is a design choice of the game). An opt-in
  `persistence.autosave` could reuse the same timer for the save file; see Q5.

### 4.5 Event re-evaluation

Today every `Set` scans every event. With compiled expressions the engine knows **which variables
each event condition reads**:

- At load, build an index `variable → events whose condition reads it`.
- A write marks only those events dirty.
- Dirty events are re-evaluated **once per frame**, before the event engine runs, so ten writes in
  one action list cost one re-evaluation.
- Conditions may read every scope, including `settings.` and `system.`. `system.` values that
  change without a write (for example `system.can_save`) mark their dependants dirty when the engine
  changes the underlying state.

### 4.6 Classes

- `VariableDefinition` (name, scope, type, default, rules, binding).
- `Value`: a small readonly struct (`Int` / `Bool` / `String`). Used at the edges (files, JSON, the
  validator). The hot path does not box: compiled expressions read typed slots.
- `VariableRef`: a name resolved at compile time to scope + slot index, so a compiled expression reads
  an array element instead of looking up a string in a dictionary.
- `VariableRegistry` (static facade over one `VariableStore` per scope): `Get`, `Set`, rule
  enforcement, change notification, dirty tracking, bindings.
- `PersistenceScheduler`: the debounce timers and the flush.

### 4.7 Behaviour documented, not changed

Writing `game.` variables from a menu while the map is below it: the affected events become dirty
and pending, and run when the game scene is on top and the map is idle again. This is the wanted
behaviour (an RPG "spend points" screen) and goes into the README.

---

## 5. Part C — Expressions

The condition language becomes a typed expression language. The same compiler serves conditions
(`if`, `visibleIf`, `enabledIf`, `lockedIf`, `branch` cases), action values (`set` with `expr`),
block properties that accept expressions (`bar.max`, `effect.intensity`) and checks in `--validate`.

### 5.1 Grammar (lowest precedence first)

```
expr        := ternary
ternary     := or ( '?' expr ':' expr )?
or          := and ( '||' and )*
and         := equality ( '&&' equality )*
equality    := relational ( ( '==' | '!=' ) relational )*
relational  := additive ( ( '<' | '<=' | '>' | '>=' ) additive )?
additive    := multiplicative ( ( '+' | '-' ) multiplicative )*
multiplicative := unary ( ( '*' | '/' | '%' ) unary )*
unary       := ( '!' | '-' ) unary | primary
primary     := INT | STRING | 'true' | 'false' | NAME | NAME '(' args? ')' | '(' expr ')'
STRING      := '...' with \' and \\ escapes
```

### 5.2 Types

Static typing, checked when the expression is compiled (at load and in `--validate`), using the
declared variable types:

| Operator | Operands | Result |
|---|---|---|
| `+ - * / %` | int, int | int (integer division; `/` and `%` by zero throw) |
| `+` | string, string or int | string (concatenation; an int is converted to text) |
| `== !=` | same type | bool |
| `< <= > >=` | int, int or string, string (ordinal) | bool |
| `! && \|\|` | bool | bool (short-circuit) |
| `c ? a : b` | bool, same type | that type |

**Ints are no longer booleans.** `!game.met_shadow` requires `met_shadow` to be `bool`. Existing
content that uses ints as flags (`talked_mother`, `clara_gone`...) is migrated to `bool` variables.
This turns a typo such as `game.courage && ...` into a compile error instead of a silent truthy test.

### 5.3 Functions

`min(a,b)`, `max(a,b)`, `abs(a)`, `clamp(v,lo,hi)`, `len(s)`, `lower(s)`, `upper(s)`,
`contains(s,part)`, `startsWith(s,p)`, `endsWith(s,p)`, `text(key)` (the translated text of a key),
`rand(lo,hi)`.

`rand` is **only allowed in action values**, not in conditions: conditions are re-evaluated whenever
their variables change, so a random condition would make events and visible elements flicker. The
validator rejects it there.

### 5.4 Errors

- Syntax errors, unknown names, type mismatches and `rand` in a condition are reported at load
  (the game does not start) and by `--validate`, with the expression and where it is used.
- Runtime errors (division by zero, int overflow, which is `checked`) throw an `ExpressionException`
  with the expression text and the current values of the variables it reads. Consistent with Q7:
  content mistakes should be loud.

### 5.5 Implementation

Lexer → AST → type check → compiled closures **per result type** (`Func<int>`, `Func<bool>`,
`Func<string>` over the registry's slots), cached by expression text. The AST also yields the
variables read (for the dependency index, §4.5) and is what `--validate` inspects.

---

## 6. Part D — Menus, controls and actions

v1 §5 stays valid except for focus and rendering (now §3.6 and §3.9) and for the points below.

### 6.1 Controls

| Type | Variable type | Keys (actions) |
|---|---|---|
| `button` | none; runs `actions` | `ui.confirm` |
| `switch` | bool | `ui.confirm`, `ui.left`/`ui.right` toggle |
| `slider` | int, `min`/`max`/`step` from the control or the template | `ui.left`/`ui.right` |
| `combobox` | int or string; `options` or the template's `options`/`optionsFrom` | `ui.left`/`ui.right` cycle (no drop-down) |
| `numberfield` | int | `ui.left`/`ui.right` by `step`; `ui.confirm` starts typing digits |
| `textfield` | string | `ui.confirm` starts editing; while editing, raw keys; Enter confirms, Escape cancels |
| `keybind` | an input action (§7.4) | `ui.confirm` starts listening |

- Every control can have `onChange` actions (for example a sound when a slider moves) and the theme
  defines default sounds for focus move, confirm and locked (`theme.json → sounds`), so menus sound
  right without an `sfx` action on every button.
- A combobox shows the template default when the values file has none yet (Q6 of v1).
- Typed characters are limited to the selected font's character map (§3.11).

### 6.2 Text

- `text` holds a localisation key; `textVar` shows a variable (translated if its value is a key).
- New inline code **`{v:game.player_name}`** prints a variable inside any text. Measured with the
  variable's declared limit (§3.4).

### 6.3 One action model

Events, buttons, control changes, scene `inputs`, `onEnter`/`onResume`/`onLeave` scene hooks and
`timer` blocks all run **action lists** through one `ActionRunner`. Actions run in order; an action
that waits (a dialog, a scene opened with `wait`, a black screen) suspends the list until it
finishes. Any action can have an `if` condition.

| Action | Effect |
|---|---|
| `set` `{var, value \| expr}` | Write a variable. `value` is a JSON literal; `expr` is an expression (`"expr": "game.gold - 10"`). |
| `add` `{var, value \| expr}` | Shortcut for `set var = var + value` (ints). |
| `sfx` `{id}`, `music` `{id}` | Sound and music. |
| `open` `{scene, params?, wait?}`, `goto`, `back`, `closeAll` | Scene stack (§3.1). |
| `focus` `{target?, mode}` | Focus changes (§3.6). |
| `message` `{keys}`, `dialog` `{id}`, `black` `{...}`, `teleport` `{map, x, y}` | Game actions (§3.12). |
| `newGame`, `continue`, `save` `{onSuccess, onFail}`, `returnToTitle`, `quit`, `endGame` | Game flow. |
| `resetBinding` `{action}`, `resetAllBindings` | Keybindings (§7). |

**Buttons with several actions** are just buttons whose `actions` list has several entries, for
example: set two variables, play a sound, close the menu.

```jsonc
{ "type": "button", "label": "shop.buy_potion", "enabledIf": "game.gold >= 10",
  "actions": [
    { "type": "add", "var": "game.gold", "value": -10 },
    { "type": "add", "var": "game.potions", "value": 1 },
    { "type": "sfx", "id": "coin" }
  ] }
```

The JSON shape of an action is one record with a `type` discriminator, shared with `events.json`
(`Json.Options` gets `AllowOutOfOrderMetadataProperties = true`, so `type` need not be the first
property).

### 6.4 Branch

`{ "type": "branch", "mode": "first", "cases": [ { "if": ..., "content": ... } ], "else": ... }`.
`mode: "first"` (default) shows the first true case; `mode: "all"` shows every true case, stacked in
order (`else` only when none is true).

---

## 7. Part E — Input actions and keybindings

### 7.1 Model

Blocks and scenes never look at keys. They react to **actions**. Keys are bound to actions in data.

```jsonc
// assets/data/input/actions.json — declarations and defaults
{
  "ui.up":      { "label": "input.up",      "keys": ["Up", "W"],      "context": "ui" },
  "ui.down":    { "label": "input.down",    "keys": ["Down", "S"],    "context": "ui" },
  "ui.left":    { "label": "input.left",    "keys": ["Left", "A"],    "context": "ui" },
  "ui.right":   { "label": "input.right",   "keys": ["Right", "D"],   "context": "ui" },
  "ui.confirm": { "label": "input.confirm", "keys": ["Space", "E"],   "context": "ui" },  // + Enter, fixed
  "ui.back":    { "label": "input.back",    "keys": ["Escape"],       "context": "ui" },
  "map.up":     { "label": "input.walk_up", "keys": ["Up", "W"],      "context": "map", "repeat": true },
  // ... map.down / map.left / map.right / map.interact
  "pause":      { "label": "input.pause",   "keys": ["Escape"],       "context": "map" },
  // A game-specific action: declared here like the built-in ones
  "inventory":  { "label": "input.inventory", "keys": ["I"],          "context": "map" }
}
```

- `keys` are the **defaults**. Up to `maxKeys` (default 2) keys per action.
- `context` groups actions that can be active at the same time. The same key may be bound in
  different contexts (`W` walks on the map and moves up in menus) but not twice in the same
  context. `global` conflicts with every context.
- `repeat: true` actions also report "held" and auto-repeat (map walking needs the held state that
  `MapScene` reads with `IsKeyDown` today).
- `rebindable: false` hides an action from the controls page.
- `InputMap` gives blocks `Pressed(action)`, `Held(action)`, `Released(action)` each frame.
- Engine actions (`ui.*`, `map.*`, `pause`) must exist; the validator checks it and checks every
  label key.

### 7.2 Enter is fixed

**Enter** is not bindable and not listed: it always means `ui.confirm` and is what starts listening
on a `keybind` control. While listening, **pressing Enter again cancels** (Enter cannot be bound, so
it is free for that), and every other key, including Escape, can be bound. See Q8.

### 7.3 Player overrides

- `keybindings.json` in the user data folder stores **only the actions that differ** from the
  defaults. Restoring an action deletes its entry; restoring all deletes the file.
- Written with the same debounce as settings (§4.4).
- On load, an override with an unknown key name or an unknown action is dropped with a warning.
  An override that would create a conflict in its context is dropped as well (the default wins).

### 7.4 The controls page

`scenes/controls.json` is an ordinary modal scene: a `menu` with one `keybind` control per
rebindable action (or a `keybind` per key slot), and buttons running `resetAllBindings` and `back`.

A `keybind` control shows the action label and its current keys (key names are localised:
`key.Up` → "↑", `key.Space` → "Espacio"...). Behaviour:

1. `ui.confirm` on the control → **listening** state ("Press a key..."). The Enter press that
   started it is ignored; listening begins the next frame.
2. While listening, the control captures all raw keys (like a text field being edited).
3. The first newly pressed key other than Enter is bound to the slot. If another action of the same
   context already uses it, the two **swap** (the other action receives the old key of this one), so
   no action is ever left without its key and no conflict is ever saved. See Q7.
4. Enter cancels.

### 7.5 Custom actions: global file or per-scene?

The question was whether custom actions should live in the settings (global) file, or whether each
scene should declare its own special actions.

**Decision: declare every action, built-in or custom, in the global `actions.json`; scenes and blocks
only declare what they do when an action fires** (`inputs: { "inventory": [ ...actions ] }`).

Reasons:

- The controls page must list every rebindable action. With per-scene declarations it would have to
  load every scene to find them.
- Conflicts can only be checked when all the actions of a context are known in one place.
- Defaults, restore and the overrides file are one table instead of one per scene.
- Two scenes reacting to the same action (an `inventory` key working on the map and in a shop) is
  natural with one declaration and handlers in each scene; with per-scene actions it would be two
  actions bound twice.

What per-scene declarations would have given (actions local to one screen) is covered by
`context`, and by scenes simply not handling actions they do not care about.

### 7.6 Raw keys

`textfield` (editing) and `keybind` (listening) are the only consumers of raw keys. Everything else,
including the map, the dialog and the title skip, goes through actions. `Scene.Confirm` (Enter /
Space / E) and every `Keys.*` in scenes and `Menu` disappear.

### 7.7 No mouse

`SadConsole.Settings.Input.DoMouse = false` at start-up, and the system cursor is hidden over the
window. Nothing in the engine reads the mouse.

---

## 8. Folder structure

### 8.1 Source

```
src/Relato/
  Config/        GameDefinition.cs (game.json), Theme.cs (theme.json)
  Variables/     VariableDefinition.cs, Value.cs, VariableRef.cs, VariableStore.cs,
                 VariableRegistry.cs, Bindings.cs, PersistenceScheduler.cs
  Expressions/   Lexer.cs, Ast.cs, Parser.cs, TypeChecker.cs, Compiler.cs, Functions.cs,
                 Expression.cs (facade + cache), ExpressionException.cs
  Input/         InputActionDefinition.cs, InputMap.cs, KeyBindingStore.cs, KeyNames.cs
  Actions/       ActionDefinition.cs, ActionRunner.cs
  Layout/        Size.cs (auto / % / cells), LayoutEngine.cs, PercentDistribution.cs,
                 TextLayout.cs (wrap + inline codes, extracted from TextBox), TextMetrics.cs
  Scenes/        SceneDefinition.cs, Scene.cs (the one generic scene), SceneStack.cs (was GameRoot),
                 ScenePresentation.cs, FocusManager.cs, SceneRepository.cs
  Blocks/        Block.cs, StackBlock.cs, OverlayBlock.cs, TextBlock.cs, TextBoxBlock.cs,
                 ImageBlock.cs, MapBlock.cs, ConversationBlock.cs, ChoicesBlock.cs, BranchBlock.cs,
                 BarBlock.cs, EffectBlock.cs, TimerBlock.cs, SpacerBlock.cs, MenuBlock.cs
  Blocks/Controls/  Control.cs, Button.cs, Switch.cs, Slider.cs, ComboBox.cs, NumberField.cs,
                 TextField.cs, KeyBind.cs
  Logic/         EventDefinition.cs, EventEngine.cs, EventScheduler.cs (dependency index, §4.5),
                 Conversation.cs
  Core/, Maps/, Rendering/, Audio/   as today, minus what moves above
tests/Relato.Tests/   Expressions, layout rounding, variable rules, input conflicts
```

### 8.2 Assets

```
assets/data/
  game.json
  variables/game.json, settings.json, session.json
  input/actions.json
  scenes/title.json, game.json, pause.json, settings.json, controls.json, dialog.json,
         black.json, exit.json
  ui/theme.json
  generated/text_metrics.csv          (written by --validate; do not edit)
  dialogs.csv, events.json, conversations.json, maps/   (migrated to prefixed names and bools)
user data: settings.json (settings values), keybindings.json, save.json
```

### 8.3 Deleted

`Scenes/TitleScene.cs`, `PauseScene.cs`, `SettingsScene.cs`, `MapScene.cs`, `DialogScene.cs`,
`BlackScene.cs`, `ExitScene.cs` (their logic moves into blocks), `UI/Menu.cs` (into controls),
`Core/UserSettings.cs` (into settings variables + bindings), `Logic/Condition.cs` (into
`Expressions/`), `Logic/InfoEngine.cs` (split into `VariableRegistry` and `EventScheduler`), the
constants of `GameConfig.cs` (into `game.json`; what remains are engine constants).

### 8.4 Modified

| File | Change |
|---|---|
| `Program.cs` | Load `game.json` (font, grid); mouse off; fullscreen from the settings variable; flush on exit. |
| `Core/GameServices.cs` | Load the new repositories and services. |
| `Core/SaveGame.cs` | `Map`, `X`, `Y` (global), `Variables: Dictionary<string, JsonElement>` (game scope), completed and pending events. |
| `Core/Json.cs` | `AllowOutOfOrderMetadataProperties = true`. |
| `Core/Paths.cs` | New folders and user files. |
| `Core/ContentValidator.cs` | Variables, expressions (types, undeclared, unused), scenes (references, params, layout at the minimum grid), input actions, text metrics generation and staleness, font character map. |
| `Core/Display.cs` | Grid computation on mode change (§3.10). |
| `Core/Cp437.cs` | Becomes the default character map behind a `CharMap` abstraction. |
| `UI/TextBox.cs` | Uses `TextLayout`; supports `{v:}`; re-pagination on resize. |
| `UI/Frame.cs` | Colours from the theme. |
| `Rendering/ImageCanvas.cs` | Fit modes and position. |
| `Rendering/DrawCursor.cs` | `SetDuration(seconds)`. |
| `Logic/EventDefinition.cs`, `EventEngine.cs` | Actions use the shared `ActionDefinition` / `ActionRunner`; `IEventHost` targets the scene stack. |
| `README.md`, `gdd.md` | Document all of the above. |

---

## 9. Remaining items from v1, checked

| v1 item | Status in v2 |
|---|---|
| Do not use `SadConsole.UI` | Still decided. |
| Three-layer menu surfaces for text over images | Superseded: each block has its own surface, ordered by z-order, so text over an image is a later block in an `overlay`. |
| Theme colours in config | `ui/theme.json` (colours + default sounds). |
| `menu` event action | Superseded by `open` with `wait` (§6.3). |
| `onBack` per menu | Scene `inputs: { "ui.back": [...] }`; the title simply does not handle it. |
| Skipping image animations with any key | `image.skippable`: completes on `ui.confirm`/`ui.back` (any action, not any key). |
| WASD in menus (Q5) | Settled by keybindings: `W/A/S/D` are default alternates of `ui.*`, rebindable. |
| Q3 (conditions reading settings/system) | Yes, every scope is readable (§4.5). |
| `DrawCursor` duration | Kept (`image.duration`). |

---

## 10. Implementation order

Your proposed order was variables → blocks → scene behaviour. I keep it, with input inserted before
the blocks and the risky platform work isolated at the end. Each phase ends with the game building,
running and passing `--validate`.

1. **Foundations.** `game.json` (editable size, start scene, new-game setup, theme), new folders,
   mouse off, test project. Small, and everything after it reads from `GameDefinition`.
2. **Variables + expressions** (together: typed variables are useless without typed expressions).
   Templates, registry, bindings (replacing `UserSettings`), debounced persistence, dependency index,
   new save format, migration of all content to prefixed names and `bool` flags. The current C#
   scenes keep working on top of the new registry.
3. **Input actions.** `actions.json`, `InputMap`, overrides file; replace every hard-coded key in the
   current scenes. Done before blocks so that no block is ever written against raw keys.
4. **Layout + text metrics.** `TextLayout` extraction, `TextMetrics` and its generated file,
   measure/arrange, percentage distribution. Pure logic, unit-tested.
5. **Scene/block runtime + menus.** Generic `Scene`, scene stack with only the top enabled,
   presentation and the modal preset, focus manager, containers, text, image (fit modes), branch,
   controls, the shared `ActionRunner`. Rebuild title, pause and settings as data; delete their C#
   classes.
6. **Game blocks.** `map` (section size from config, global positions), `textbox` messages,
   `conversation` + `choices`, `bar`, `effect`, `timer`; events through the action runner and scene
   params. Rebuild game, dialog, black and exit as data; delete their C# classes.
7. **Controls page.** `keybind` control, conflicts with swap, reset actions, `scenes/controls.json`.
8. **Window-driven grid + fonts + culling.** Prototype runtime grid changes in SadConsole first
   (§3.10 risk), then grid computation on mode change, custom font and character map, block culling.

---

## 11. Limitations (updated)

| Limitation | Cause | Status |
|---|---|---|
| No drop-down combo box | Decision | Cycling `◄ value ►`. |
| No mouse | Decision: keyboard-only terminal feel | Mouse processing disabled. |
| Only bitmap tile fonts | SadConsole renders tile sheets | Custom `.font` supported; TTF needs an external converter. |
| Characters limited to the font's character map | One glyph per cell | Validator checks texts, maps and defaults. |
| One font size per surface | SadConsole | Each block that needs another size (image, big text, small map) has its own surface. |
| Images cannot shrink below the minimum character size | Art is not down-sampled | Cut + warning; validator checks at the minimum grid. |
| Runtime grid change not native | SadConsole assumes a fixed start grid | Prototype in phase 8; fallback is today's fixed grid scaled with `Fit`. |
| No collections (arrays) in variables | Not in scope | Next step after this redesign; needed for inventories (`list` block). |
| Integer-only maths | Decision | Enough for every current and planned use. |

---

## 12. Open questions

Each has a recommendation; the design above assumes the recommended answer.

1. **Q1 Runtime writes out of range.** Loads reset to the default (as decided). For writes during
   play: clamp ints and reject invalid strings (recommended), or also reset to the default?
2. **Q2 Prefix everywhere.** Must every reference use its scope prefix, including the many game
   conditions (`game.courage >= 2`) (recommended: explicit, no ambiguity), or should an unprefixed
   name mean `game.`?
3. **Q3 Pause and settings as separate modal scenes** (recommended, reused by title and game), or as
   blocks inside the game scene?
4. **Q4 One player map.** Only one map block owns the player; others are views (recommended). Is
   there a case you have in mind that needs two controllable maps?
5. **Q5 Autosave.** Game values are written only by the `save` action (recommended for this game).
   Should the engine offer an opt-in `autosave` using the debounce timer?
6. **Q6 Re-layout on window resize.** Only on window-mode change, scaling in between (as you asked,
   recommended), or also after a resize ends (debounced, e.g. 300 ms)?
7. **Q7 Rebinding a key already in use.** Swap the keys (recommended) or refuse with a message?
8. **Q8 Cancelling a rebind.** Enter cancels, so Escape stays bindable (recommended), or Escape
   cancels and becomes non-bindable too?
