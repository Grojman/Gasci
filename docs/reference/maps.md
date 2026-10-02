# Maps

A map is two files in `assets/data/maps/`:

- `<id>.txt`: the **drawing**, UTF-8 text. One character per cell.
- `<id>_info.json`: what the characters of the drawing **mean**.

```
###########################o################################
#bbb............#......................#.....fff....kkkkkk.#
#bbb............#......................#.....fff...........#
#...............#.........ppp..........#...................#
```

Characters not listed in `tiles` are walkable and drawn as they are.

## Sections

A map can be much bigger than the screen. It is split into **sections** of
`game.json → map.sectionWidth × map.sectionHeight` cells (98×34 in the demo); the map block shows one
section at a time and loads the next when the player walks off the edge. The frame shows arrows where
there is more map. Section size does not depend on the screen, so positions, exits and `halfway`
triggers mean the same on every monitor.

Positions in the info file, in events and in `teleport` are **global** cells of the whole drawing
(`x` = column, `y` = row, from 0).

## `<id>_info.json`

```jsonc
{
  "titleKey": "map.street",
  "textbox": "map_message",
  "music": "street",
  "spawn": [8, 5],
  "variants": [ { "if": "game.distortion >= 2", "file": "street_distorted" } ],
  "tiles": [
    { "char": "#", "glyph": "█", "fg": "#2f2f3a", "solid": true },
    { "char": "w", "glyph": "▒", "fg": "#6b6030", "solid": true, "interact": "obj.window_street" },
    { "char": "l", "anim": ["☼", "☼", "☼", "∙"], "animSpeed": 0.25, "fg": "#d8c070", "solid": true, "interact": "obj.lamp" }
  ],
  "exits": [ { "x": 10, "y": 6, "map": "home", "toX": 27, "toY": 12 } ],
  "npcs": [
    { "id": "clara", "x": 61, "y": 8, "glyph": "C", "fg": "#c9a0dc", "dialog": "clara_talk",
      "variants": [
        { "if": "game.clara_gone", "glyph": "%", "fg": "DarkRed", "dialog": "", "interact": "obj.clara_gone" },
        { "if": "game.clara_helped", "fg": "#a0dca0", "dialog": "clara_after" }
      ] }
  ]
}
```

| Property | Meaning |
|---|---|
| `titleKey` | Text key of the map name, printed on the map block's top frame. |
| `textbox` | Id of the `textbox` block where the interaction texts of this map are shown. Tiles and NPCs may override it. **Required** if anything on the map has an interaction text. |
| `music` | Music played when the map loads. |
| `spawn` | `[x, y]` where the player appears when no position is given. Default `[1, 1]`. Must not be solid. |
| `variants` | Alternative drawings: `[{ "if": condition, "file": "<other drawing>" }]`. The first true one is used, and the map switches as soon as the condition changes. Variants must be the same size as the base drawing. |
| `tiles` | What characters mean (below). |
| `exits` | Cells that move the player to another map (below). |
| `npcs` | Characters on the map (below). |

### Tiles

| Property | Default | Meaning |
|---|---|---|
| `char` | — | The character used in the `.txt` file. One character. |
| `glyph` | `char` | The character drawn on screen. |
| `fg`, `bg` | — | Colours. |
| `solid` | `false` | The player cannot walk on it. |
| `interact` | — | Text key shown when the player bumps into the tile or presses interact facing it. |
| `textbox` | map's | Textbox for `interact`. |
| `anim` | — | Animation frames (glyphs), cycled every `animSpeed` seconds. |
| `animSpeed` | `0.5` | Seconds per frame. |

### Exits

| Property | Default | Meaning |
|---|---|---|
| `x`, `y` | — | The exit cell. Must not be solid. |
| `map` | — | Destination map. |
| `toX`, `toY` | — | Destination cell. Must not be solid, nor another exit. |
| `sfx` | `"door"` | Sound played; `null` for none. |
| `if` | — | Condition for the exit to work. |

### NPCs

| Property | Meaning |
|---|---|
| `id` | Name of the NPC (for messages and the validator). |
| `x`, `y` | Position. |
| `if` | Condition for the NPC to exist on the map. |
| `glyph`, `fg` | How it is drawn. |
| `anim`, `animSpeed` | Animation frames instead of a fixed glyph. |
| `dialog` | Conversation started when the player interacts. |
| `interact` | Text key shown when there is no conversation. |
| `textbox` | Textbox for `interact`. |
| `variants` | `[{ "if": condition, ...overrides }]`. The **first** true variant overrides the fields it sets (`glyph`, `fg`, `anim`, `animSpeed`, `dialog`, `interact`, `textbox`); the rest come from the base. Put the most important variant first. `"dialog": ""` removes the base conversation (so `interact` is used). |

NPCs block the way. Bumping into an NPC or a solid tile interacts with it, but only on a fresh key
press, so holding a key does not reopen a conversation as soon as it closes.

## The map block

Maps are shown by a [`map` block](blocks.md#map). The block with `"player": true` owns the player: it
moves with the `map.*` input actions, interacts with `map.interact` (or `ui.confirm`), is stored in the
save and receives `teleport`. With `"raiseEvents": true` it fires the `enterMap`, `step` and `halfway`
[triggers](events.md).

## Validation

`--validate` checks that drawings load, variants have the base size, tile chars are one character, the
spawn is not solid, exits and their destinations are valid, NPCs are inside the map and their
conversations exist, every interaction text has a textbox, and every character can be drawn by the font.
