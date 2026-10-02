# Architecture

This page explains how the engine is organised, for people changing the C# code. Authors of content
only need the [content reference](../README.md#documentation). The reasoning behind each decision is in
the [design notes](design/ui-system-design.md).

## Overview

```
game.json ──► GameDefinition (grid, font, start scene, new-game setup)
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

## Code layout (`src/Gasci`)

| Folder | Contents |
|---|---|
| `Program.cs` | Entry point: `--validate`, or load content, configure SadConsole and run. |
| `Config/` | `GameDefinition` (game.json) and `Theme` (ui/theme.json). |
| `Variables/` | Typed values (`Value`), templates (`VariableDefinition`), the `VariableRegistry` (scopes, rules, change notification), engine `Bindings` and the debounced `PersistenceScheduler`. |
| `Expressions/` | The expression language: `Lexer`, `Parser` (→ `Ast`), the type-checking `Compiler` and the cached `Expression` facade. |
| `Input/` | `InputMap`: action definitions, default keys, player overrides, rebinding with swap, pressed / held state per frame. |
| `Actions/` | `ActionDefinition` (the single action shape) and the `ActionRunner`, which runs lists and suspends on waiting actions. |
| `Layout/` | `Size` (auto / fill / % / cells), anchors, percentage distribution, `TextLayout` (wrapping and inline codes), `TextMetrics`. |
| `Scenes/` | `SceneDefinition` and presentation, the generic `Scene`, the `SceneStack` (root of the screen and game flow) and the `FocusManager`. |
| `Blocks/` | Every block type; `Blocks/Controls/` holds the menu controls. |
| `Logic/` | `EventDefinition`, the `EventEngine`, conversations. |
| `Maps/` | `MapInfo` (the `_info.json` format), `MapData` (a loaded map with its variants), `MapService`. |
| `Rendering/` | `AsciiImage`, `ImageService`, `ImageCanvas` (fit modes) and `DrawCursor` (animated drawing). |
| `Audio/` | `AudioService` (music and sound effects, `.wav` overrides) and `Synth` (the synthesized placeholders). |
| `UI/` | The `TextBox` widget (typewriter, pages) and `Frame` drawing helpers. |
| `Core/` | Paths, JSON options, log, localisation, character map, font check, display modes, save files, `GameServices` and the `ContentValidator`. |

## Start-up

1. `Program` checks for `--validate` (then runs `ContentValidator` and exits).
2. `GameServices.LoadContent()` loads, in order: `game.json`, the theme, the custom font and its
   character map, `dialogs.csv`, the `system.*` values, the variable templates, text metrics, input
   actions, images, maps, conversations and events. Any `ContentException` stops the game with the
   error in the log.
3. `GameServices.LoadUserFiles()` loads `settings.json` and `keybindings.json`.
4. `Display.Configure` computes the window and grid from `game.json` and the font glyph size.
5. SadConsole starts. The starting screen is the `SceneStack`, which creates **every** scene once
   (block ids are unique in the whole game), installs the bindings (applying volumes, language and display
   mode) and opens `startScene`.

## The frame

`SceneStack.Update` runs once per frame:

1. `InputMap.Update` reads the keyboard and computes pressed / held / released per action.
2. The scenes update. Only the **top** scene is enabled; the ones below are frozen.
3. The top scene dispatches the input actions to its focused blocks (topmost first; a block that
   handles an action consumes it unless it has `passInput`), then to its own `inputs`.
4. `EventEngine.Update` runs ready events (see below).
5. `PersistenceScheduler.Tick` writes settings / key bindings whose debounce expired; autosave ticks.

Changes to the stack requested while scenes update (open, back, goto...) are queued and applied between
those steps, so a scene never disappears in the middle of its own update.

## Scenes and blocks

- A scene is one generic `Scene` class plus a JSON tree of blocks. Blocks are deserialized by their
  `type` (`[JsonDerivedType]` on `Block`).
- Layout has two passes: `Measure(available)` bottom-up returns the size a block wants;
  `Arrange(rect)` top-down gives it its rectangle. Layout reruns only when it is invalidated (language
  change, display mode change, visibility changes).
- Each block that draws owns a SadConsole surface. Blocks redraw only when what they show changed (the
  map and effects redraw every frame). A block entirely covered by an opaque block above it is culled,
  and scenes hidden behind an opaque scene are not drawn.
- Opening a scene again re-applies its parameters and resets its blocks (children before parents, so
  composite blocks drive their slots after the slots reset).

## Variables and expressions

- Values live in memory in typed slots, per scope. Every write goes through `VariableRegistry.Set`,
  which applies the rules, notifies listeners (bindings, events, blocks) and marks the scope dirty for
  persistence.
- Expressions are compiled once into typed closures (`Func<int>`, `Func<bool>`, `Func<string>`) and cached
  by their text. Compilation also returns the variables an expression reads.

## Events

- Triggered events live in a dictionary keyed by trigger type and map / block. A trigger looks only at
  its own list and checks each condition at that moment.
- `immediate` events are indexed by the variables their condition reads. A write marks the affected
  events dirty; dirty events are re-checked once per frame. Conditions reading `system.*` are checked
  every frame, since those values change without a write.
- Events only run while the top scene has a focused, idle block with `raiseEvents`. A ready event
  re-checks its condition right before running.
- An event's actions run through the same `ActionRunner` as buttons. Waiting actions (`dialog`,
  `black`, `message`, `open` with `wait`) suspend the list until they finish.

## Adding to the engine

| To add | Touch |
|---|---|
| A block | A `Block` subclass in `Blocks/`, a `[JsonDerivedType]` line in `Block.cs`, its checks in `ContentValidator.CheckBlock`, and [`docs/reference/blocks.md`](reference/blocks.md). |
| An action | A constant in `ActionTypes` (and in `ActionTypes.All`), its fields in `ActionDefinition`, its case in `ActionRunner`, its checks in `ContentValidator` (the `switch` over action types), and [`docs/reference/actions.md`](reference/actions.md). |
| A trigger | `TriggerTypes`, `EventTrigger` fields, `EventEngine` indexing and notification, validator, [`docs/reference/events.md`](reference/events.md). |
| An expression function | Its signature in `Compiler.Functions` and its case in the compiler, a test in `ExpressionTests`, [`docs/reference/expressions.md`](reference/expressions.md). |
| A binding | A constant in `Bindings`, its handler in `GameServices.InstallBindings`, [`docs/reference/variables.md`](reference/variables.md). |
| A `system.*` value | `GameServices.DefineSystemValues`, [`docs/reference/variables.md`](reference/variables.md). |

Then add a `CHANGELOG.md` entry (a new feature is a MINOR version; see [versioning](versioning.md)).
