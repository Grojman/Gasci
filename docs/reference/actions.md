# Actions

Everything that *does* something is an **action list**. The same format is used by:

- [events](events.md) (`actions`),
- buttons (`actions`) and controls (`onChange`),
- scene and block `inputs`,
- scene hooks (`onEnter`, `onResume`, `onLeave`),
- timers (`actions`), effects (`onDone`), conversations and cards (`onEnd`), choice items (`actions`),
- `save` (`onSuccess`, `onFail`).

```jsonc
"actions": [
  { "type": "add", "var": "game.gold", "value": -10 },
  { "type": "add", "var": "game.potions", "value": 1, "if": "game.potions < 9" },
  { "type": "sfx", "id": "coin" }
]
```

- Actions run **in order**.
- Any action may have an **`if`** condition; when it is false the action is skipped.
- Some actions **wait** (a message, a dialog, a black card, an `open` with `wait`): the rest of the list
  runs when they finish. That is how an event can show a text, then a conversation, then a card.

## Reference

### Variables

| Action | Fields | Effect |
|---|---|---|
| `set` | `var`, `value` or `expr` | Writes a variable. `value` is a JSON literal of the variable's type; `expr` is an [expression](expressions.md) (`"expr": "game.gold - 10"`). Out-of-range ints are clamped, invalid strings rejected (see [rules](variables.md#rules)). |
| `add` | `var`, `value` or `expr` | Adds an int to an int variable (`"value": -1` subtracts). |

### Sound

| Action | Fields | Effect |
|---|---|---|
| `sfx` | `id` | Plays a sound effect. |
| `music` | `id` | Plays a music track; `""` (or no `id`) stops the music. |

### Scenes

| Action | Fields | Effect |
|---|---|---|
| `open` | `scene`, `params`, `wait` | Pushes a scene on the stack with its parameters. With `"wait": true` the list waits until that scene closes. |
| `goto` | `scene`, `params` | Replaces the scene that ran the action with another, in the same place of the stack. |
| `back` | — | Closes the scene that ran the action; the scene below resumes. |
| `closeAll` | — | Closes every scene above the bottom one. |

```jsonc
{ "type": "open", "scene": "shop", "params": { "owner": "clara" }, "wait": true }
```

### Focus

| Action | Fields | Effect |
|---|---|---|
| `focus` | `target`, `mode` | Changes [focus](scenes.md#focus). `mode`: `move` (default; the origin loses focus, the target gains it), `share` (both keep it), `release` (the origin loses it; no `target`). The target must be in the same scene. |

### Game

| Action | Fields | Effect | Waits |
|---|---|---|---|
| `message` | `target`, `keys` or `key` | Shows texts, one after another, in the textbox block `target`. The textbox must be in the **top** scene. | until read |
| `dialog` | `id` | Opens the conversation scene (`game.json → conversationScene`) with that [conversation](conversations.md). | until it ends |
| `black` | `key`, `image`, `seconds`, `big`, `drawMode`, `cursorMode` | Opens the card scene (`game.json → cardScene`): an image and / or a text on black. With `seconds` it closes on its own that long after it finishes drawing; without, the player confirms. `big` shows the text at scale 2. | until closed |
| `teleport` | `map`, `x`, `y` | Moves the player (the map block with `"player": true`) to a map and position. | — |

```jsonc
{ "type": "black", "key": "ev.intro.2", "image": "general/eye", "drawMode": "Inside" }
```

### Game flow

| Action | Effect |
|---|---|
| `newGame` | Resets every `game.*` variable and the events, closes every scene, and opens `game.json → newGame`. |
| `continue` | Loads `save.json`: game variables, events, the saved scenes and their block state. |
| `save` | Saves the game. Runs `onSuccess` or `onFail` (actions) afterwards. It fails when no saved scene is open or an event is running (`system.can_save`). |
| `returnToTitle` | Writes pending settings, aborts running events, closes every scene and opens `startScene`. Does **not** save: put a `save` before it if wanted. |
| `quit` | Opens `game.json → quitScene` (an exit effect), or closes at once when there is none. |
| `exit` | Writes pending settings and closes the game at once. The last action of a quit scene. |
| `endGame` | Deletes the save and returns to the title (the end of the story). |

```jsonc
{ "type": "save",
  "onSuccess": [ { "type": "set", "var": "session.pause_status", "value": "pause.saved" } ],
  "onFail":    [ { "type": "set", "var": "session.pause_status", "value": "pause.cannot_save" } ] }
```

### Key bindings

| Action | Fields | Effect |
|---|---|---|
| `resetBinding` | `action` | Restores the default keys of one input action. |
| `resetAllBindings` | — | Restores every default (deletes the overrides). |

## Validation

`--validate` checks every action list in the game: known types, required fields, variables that exist
and have the right type, values that respect the rules, expressions, and that scenes (with their
parameters), blocks, textboxes, conversations, images, maps and input actions exist.
