# Events

Events are the story logic: *when* something happens and a *condition* holds, run some
[actions](actions.md). They live in `assets/data/events.json`, a list:

```jsonc
[
  {
    "id": "home_whispers",
    "if": "game.distortion >= 1",
    "once": false,
    "trigger": { "type": "enterMap", "map": "home" },
    "actions": [
      { "type": "sfx", "id": "door" },
      { "type": "message", "target": "map_message", "keys": ["ev.home_whispers"] }
    ]
  }
]
```

| Property | Default | Meaning |
|---|---|---|
| `id` | — | Unique. Stored in the save when the event is completed. |
| `if` | — | Condition, checked when the trigger fires and again right before the event runs. |
| `trigger` | `immediate` | What fires the event (below). |
| `once` | `true` | Completed after running, so it never runs again. With `false` it runs every time its trigger fires and its condition holds. |
| `actions` | `[]` | The actions to run. |

## Triggers

| `type` | Fields | Fires when |
|---|---|---|
| `immediate` | — | Its condition becomes true. Always runs once. |
| `enterMap` | `map` | A map block with `raiseEvents` loads that map (new game, exit, teleport, continue). |
| `step` | `map`, `x`, `y`, `w` (1), `h` (1) | The player steps inside the rectangle `x, y, w, h` (global map cells). |
| `halfway` | `map`, `section` | The player crosses the middle of a section **from left to right**. `section` is the horizontal section index (0 = first); without it, any section. |
| `input` | `block`, `action` | A block with `raiseEvents` receives an input action. Without `block`, any such block; without `action`, any action. |
| `change` | `block` | A control with `raiseEvents` changes its value. Without `block`, any such control. |

```jsonc
{ "type": "step", "map": "park", "x": 28, "y": 2, "w": 5, "h": 1 }
{ "type": "halfway", "map": "street", "section": 1 }
{ "type": "input", "block": "world", "action": "inventory" }
```

## When events run

- When a trigger fires, the events of **that trigger** whose condition is true become *ready*. Nothing
  else is evaluated: events are indexed by trigger, map and block.
- `immediate` events are indexed by the variables their condition reads. Writing one of those variables
  marks them for a check, which happens once per frame. Conditions that read `system.*` values are
  checked every frame.
- Ready events run **one at a time**, in the order they became ready, and only while the top scene has a
  **focused, idle block with `raiseEvents`**. In the demo that block is the map: events wait while a
  message is open, a conversation is on screen or the pause menu is open. An inventory menu with
  `raiseEvents` would let events run while it is open, too.
- Right before running, the condition is checked again (an earlier event may have changed things).
- Waiting actions (messages, dialogs, cards) suspend the event; the next ready event starts when it
  finishes.
- If something changes game variables while the game scene is below another scene (a menu), the affected
  immediate events wait until the game scene is on top and the map is idle again.

Completed events and ready events are part of the save.

## Patterns

**An ending chosen by a variable**: two events on the same trigger with opposite conditions.

```jsonc
{ "id": "ending_dawn",        "if": "game.courage >= 2", "trigger": { "type": "step", "map": "park", "x": 28, "y": 2, "w": 5 }, "actions": [ ... ] },
{ "id": "ending_other_shore", "if": "game.courage < 2",  "trigger": { "type": "step", "map": "park", "x": 28, "y": 2, "w": 5 }, "actions": [ ... ] }
```

**Something that changes the world for good**: an immediate event that sets a flag, and map or NPC
variants that read it.

```jsonc
{ "id": "clara_vanishes",
  "if": "game.distortion >= 3 && game.met_shadow && !game.clara_helped",
  "actions": [ { "type": "set", "var": "game.clara_gone", "value": true } ] }
```

**A custom key on the map**: declare the input action in `actions.json`, add `"raiseEvents": true` to the
map (the demo already has it), and use an `input` trigger with `"once": false`.
