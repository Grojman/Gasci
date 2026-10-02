# Variables

Variables hold the state of the game and of its settings. Every variable is **declared** in a template,
has a **type** and a **default**, and is always referenced with its **scope prefix**.

## Scopes

| Scope | Template | Stored in | Lifetime |
|---|---|---|---|
| `game.` | `data/variables/game.json` | `save.json` | Reset to defaults by *New game*; written by `save` (and autosave), read by `continue`. |
| `settings.` | `data/variables/settings.json` | `settings.json` | Kept across games; written a moment after the last change (`persistence.delaySeconds`). |
| `session.` | `data/variables/session.json` | — | Reset at start-up; never stored. Good for UI state (a status message in the pause menu). |
| `system.` | — (engine) | — | Read-only values computed by the engine. |

### `system.*` values

| Name | Type | Value |
|---|---|---|
| `system.save_exists` | bool | A save file exists (e.g. to enable *Continue*). |
| `system.can_save` | bool | A saved scene is in the stack and no event is running. |
| `system.in_game` | bool | A saved scene (the game) is in the stack. |
| `system.display_mode` | string | `windowed`, `fullscreen` or `borderless`. |
| `system.language` | string | The current language (a column of `dialogs.csv`). |

## Templates

One file per scope. Names are declared **without** the prefix (the file is the scope) and used **with**
it everywhere else. Dots after the prefix are allowed for grouping (`game.stats.strength`).

```jsonc
// data/variables/game.json — used as game.courage, game.met_shadow...
{
  "courage":     { "type": "int",    "default": 0, "min": 0, "max": 10 },
  "met_shadow":  { "type": "bool",   "default": false },
  "player_name": { "type": "string", "default": "Ana", "maxLength": 12 },
  "class":       { "type": "string", "default": "rogue", "options": ["rogue", "mage"] }
}
```

| Property | Applies to | Meaning |
|---|---|---|
| `type` | all | `int`, `bool` or `string`. There are no floats: maths is integer. |
| `default` | all | **Mandatory.** A missing default, one of the wrong type, or one that breaks its own rules stops the game from loading. |
| `min`, `max` | int | Inclusive limits. |
| `maxLength` | string | Maximum number of characters. |
| `options` | string | The only values allowed. |
| `optionsFrom` | string | An engine list of allowed values. Only `"languages"` (the columns of `dialogs.csv`). |
| `bind` | settings | Connects the variable to the engine (see [Bindings](#bindings)). |

## Rules

Values are checked in one place, so the rules always hold:

- **Writes during play** (actions, controls): ints out of range are **clamped**, invalid strings are
  **rejected** (the old value stays). Both are logged as warnings.
- **Controls** enforce the rules on screen too, so the player is never silently corrected: sliders stop
  at their limits, number fields refuse digits that would leave the range, text fields refuse characters
  past `maxLength` (playing the *locked* sound and showing a counter).
- **Values loaded from a file** (save or settings) that have the wrong type or break the rules are reset
  to the default, and logged. Values of variables that are no longer declared are dropped; declared
  variables missing from the file take their default. So an old save never breaks a newer game.
- **Undeclared variables** are errors: `--validate` reports each use, and the game refuses to load.
- `--validate` **warns** about declared variables nothing uses.

## Bindings

`bind` connects a `settings.` variable to an engine feature. When the variable changes (from a control,
an action or the settings file), the engine applies it. Each binding can be used once.

| Binding | Type | Effect |
|---|---|---|
| `audio.music` | int | Music volume = value / `max` (or / 10 without `max`). |
| `audio.sfx` | int | Sound effect volume, same formula. |
| `loc.language` | string | The current language. Use `"optionsFrom": "languages"`. Every scene is laid out again. |
| `display.mode` | string | `windowed`, `fullscreen` or `borderless`. Recomputes the grid (see [game.json](game-config.md#display-modes-and-the-grid)). |
| `save.autosave` | bool | Enables autosave. |
| `save.autosaveMinutes` | int | Minutes between autosaves (at least 1; 5 when unbound). |

```jsonc
// data/variables/settings.json
{
  "music_volume":     { "type": "int",    "default": 6, "min": 0, "max": 10, "bind": "audio.music" },
  "sfx_volume":       { "type": "int",    "default": 7, "min": 0, "max": 10, "bind": "audio.sfx" },
  "language":         { "type": "string", "default": "es", "optionsFrom": "languages", "bind": "loc.language" },
  "display_mode":     { "type": "string", "default": "windowed", "options": ["windowed", "fullscreen", "borderless"], "bind": "display.mode" },
  "autosave":         { "type": "bool",   "default": false, "bind": "save.autosave" },
  "autosave_minutes": { "type": "int",    "default": 5, "min": 1, "max": 60, "bind": "save.autosaveMinutes" }
}
```

`--validate` warns when a binding has no variable (the player could never change that setting).

## Saving

- `save` (an [action](actions.md)) writes `save.json`: every `game.*` variable, the stack of scenes marked
  `"saved": true` with their parameters and block state (the map stores its map, position and facing),
  and the completed and pending events. `continue` restores exactly that.
- Saving is only possible while a saved scene is in the stack and no event is running
  (`system.can_save`).
- **Autosave**: when the variable bound to `save.autosave` is true, the game saves silently every
  `save.autosaveMinutes` minutes while a saved scene is on top and no event is running.
- `endGame` deletes the save and returns to the title.

## Variables written from menus

Writing `game.*` variables from a menu while the game is below it (an RPG "spend points" screen) works:
the affected immediate events wait until the game scene is on top and the map is idle again, then run.
See [events](events.md#when-events-run).
