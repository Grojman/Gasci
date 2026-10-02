# Conversations

Conversations are branching dialogs with an animated portrait. They live in
`assets/data/conversations.json`, an object keyed by conversation id. They are started by the
`dialog` [action](actions.md) or by interacting with an [NPC](maps.md#npcs) that has a `dialog`, and are
shown by the [`conversation` block](blocks.md#conversation) of the conversation scene
(`game.json → conversationScene`).

```jsonc
{
  "mother_morning": {
    "character": "mother", "name": "char.mother", "color": "Wheat", "drawMode": "ZigZag", "cursorMode": "Single",
    "nodes": {
      "start": { "text": "conv.mother.greet", "image": 1, "next": "ask" },
      "ask": {
        "text": "conv.mother.ask",
        "options": [
          { "text": "opt.mother.truth",   "next": "truth", "add": { "game.trust_mother": 1 } },
          { "text": "opt.mother.lie",     "next": "lie",   "add": { "game.distortion": 1 } },
          { "text": "opt.mother.silence", "next": "silence" },
          { "text": "opt.mother.tell",    "lockedIf": "game.courage < 1", "next": "tell" }
        ]
      },
      "truth": { "text": "conv.mother.truth", "set": { "game.talked_mother": true } },
      "lie":   { "text": "conv.mother.lie", "image": 2, "set": { "game.talked_mother": true }, "next": "lie2" },
      "lie2":  { "text": "conv.mother.lie2", "image": 1, "drawMode": "Random" }
    }
  }
}
```

## Conversation

| Property | Default | Meaning |
|---|---|---|
| `character` | — | Folder in `assets/images/characters/` holding the numbered portrait frames (`1.txt`, `2.txt`...). |
| `name` | — | Text key of the name shown over the portrait. |
| `color` | `#d2c8be` | Colour of the portrait. |
| `drawMode` | `"Borders"` | Default [draw mode](media.md#draw-animation) of the portrait. |
| `cursorMode` | `"Multiple"` | Default cursor mode. |
| `music` | — | Music played during the conversation; the previous music comes back afterwards. |
| `start` | `"start"` | First node. |
| `nodes` | — | The nodes, keyed by id. |

## Nodes

When a node is entered, in this order: its `set` / `add` are applied, its `sfx` plays, its portrait is
drawn, and its `text` is typed. After the text is read, the node shows its options; without options it
follows the first true `branch`, else `next`. A node with no `next` (or a `next` that leads nowhere) ends
the conversation.

| Property | Meaning |
|---|---|
| `text` | Text key of the line. A node without text goes straight to its options / branch / next (useful for pure branching nodes). |
| `image` | Portrait frame to draw (`1` = `1.txt`). Missing keeps the current one. Frames of the same size redraw only the cells that differ. |
| `drawMode`, `cursorMode` | Override the conversation's draw animation for this frame. |
| `imageAfter` | `{ "seconds", "image", "drawMode", "cursorMode" }`: change the portrait after some seconds in the node, while the player reads or decides. |
| `set` | Variables to write: `{ "game.met_shadow": true }`. |
| `add` | Ints to add: `{ "game.courage": 1 }`. |
| `sfx` | Sound played on entering the node. |
| `options` | Answers (below). |
| `branch` | `[{ "if": condition, "next": node }]`: conditional jumps checked in order before `next`. |
| `next` | The node that follows. |

## Options

| Property | Meaning |
|---|---|
| `text` | Text key of the answer. |
| `next` | Node it leads to. Missing ends the conversation. |
| `if` | Condition for the option to be listed at all. |
| `lockedIf` | Condition under which the option is shown but cannot be chosen (the player sees what courage would unlock). |
| `lockAfter` | Seconds after which the option locks: the player hesitated. A countdown is shown. |
| `set`, `add` | Variable changes when chosen. |
| `sfx` | Sound when chosen (default: the theme's `confirm`). |

```jsonc
// A conditional opening: different first lines depending on the story.
"start": {
  "image": 1,
  "branch": [ { "if": "game.distortion >= 2", "next": "distorted" } ],
  "next": "greet"
}
```

## Portraits

Portraits are [ASCII images](media.md#images) in `assets/images/characters/<character>/<n>.txt`. Using
frames of the same size for one character makes expression changes cheap and nice to watch: only the
cells that differ are redrawn.

## Validation

`--validate` checks draw and cursor modes, that the start node and every `next` / branch target exist,
that every portrait frame exists, that every text key exists, and that `set` / `add` use declared
variables of the right type.
