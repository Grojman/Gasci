# Input

Blocks and scenes never look at keys. They react to **input actions**, and keys are bound to actions in
data. The player can rebind every action in game.

## `data/input/actions.json`

```jsonc
{
  "ui.up":        { "label": "input.up",        "keys": ["Up", "W"],     "context": "ui" },
  "ui.confirm":   { "label": "input.confirm",   "keys": ["Space", "E"],  "context": "ui" },
  "map.up":       { "label": "input.walk_up",   "keys": ["Up", "W"],     "context": "map", "repeat": true },
  "map.interact": { "label": "input.interact",  "keys": ["E", "Space"],  "context": "map" },
  "pause":        { "label": "input.pause",     "keys": ["Escape"],      "context": "map" },
  // A game-specific action, declared like the built-in ones:
  "inventory":    { "label": "input.inventory", "keys": ["I"],           "context": "map" }
}
```

| Property | Default | Meaning |
|---|---|---|
| `label` | — | Text key shown on the controls page. |
| `keys` | — | The **default** keys: primary, secondary... up to `input.maxKeys` (game.json). |
| `context` | `"global"` | Actions of the same context can be active at the same time, so they cannot share a key. `global` conflicts with every context. The same key may be used in different contexts (`W` walks on the map and moves up in menus). |
| `repeat` | `false` | The action also reports "held" and auto-repeats (walking). |
| `rebindable` | `true` | `false` hides the action from the controls page and ignores overrides for it. |

Key names are the MonoGame / SadConsole `Keys` names: `A`…`Z`, `D0`…`D9`, `NumPad0`…, `Up`, `Down`,
`Left`, `Right`, `Space`, `Escape`, `Tab`, `Back` (Backspace), `LeftShift`, `F1`…

### Required actions

The engine needs `ui.up`, `ui.down`, `ui.left`, `ui.right`, `ui.confirm`, `ui.back`, `map.up`,
`map.down`, `map.left`, `map.right` and `map.interact`. Everything else (`pause`, `inventory`...) is
game-specific: declare it here and handle it in a scene's or block's `inputs`.

**Every action is declared here, including game-specific ones**, so the controls page can list them,
detect conflicts and restore defaults. Scenes and blocks only say what they do with an action:

```jsonc
"inputs": { "inventory": [ { "type": "open", "scene": "inventory" } ] }
```

### Enter is fixed

**Enter** always means `ui.confirm`. It cannot be bound and is not listed. It is also what starts and
cancels listening on a `keybind` control, which is why every other key, including Escape, can be bound.

## How input reaches blocks

1. Every frame, the keys pressed are turned into actions.
2. The **focused** blocks of the top scene receive them, topmost first. A block that handles an action
   **consumes** it, unless it has `passInput: true`.
3. Actions no block consumed go to the scene's `inputs`.

See [Scenes → Focus](scenes.md#focus).

Only a `textfield` that is editing and a `keybind` that is listening read raw keys.

## Rebinding

The controls page is the `keybind` control ([blocks](blocks.md#keybind)): one row per action, one column
per key slot.

- Left / right choose the slot, Enter starts listening, the next key pressed is bound. Pressing Enter
  again cancels.
- A key already used by another action of the same context is **swapped**: the other action gets this
  one's old key. No action is ever left without its key, and a conflict is never saved.
- The `resetBinding` and `resetAllBindings` [actions](actions.md) restore defaults.
- `keybindings.json` in the user folder stores **only** the actions that differ from the defaults.
  Deleting it restores every default. On load, overrides with unknown actions or keys, or that would
  create a conflict, are dropped with a warning.

## Showing keys in texts

Use the `{k:action}` inline code ([texts](texts.md#inline-codes)) so hints stay right after rebinding:
`"Press {k:map.interact} to look"`. Key names are translated through the text keys `key.<Name>`
(`key.Space` → "Espacio") when they exist.
