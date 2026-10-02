# Making a game: a tutorial

This tutorial extends the demo with a small attic: a new map reachable from home, a hungry cat with a
conversation, a coin counter on screen, an event, and a *journal* opened with a new key. It touches
every kind of content file, so afterwards you can make anything else from the
[reference](../README.md#documentation).

All the steps were checked with `--validate`. Run it after each step: it tells you exactly what is
missing.

```bash
dotnet run --project src/Relato -- --validate
```

## 1. Variables

State lives in typed [variables](reference/variables.md). Add two to `assets/data/variables/game.json`
(the `game.` scope: saved with the game, reset by *New game*):

```jsonc
  "fed_cat": { "type": "bool", "default": false },
  "coins":   { "type": "int",  "default": 3, "min": 0, "max": 99 }
```

They are now `game.fed_cat` and `game.coins` everywhere. `--validate` will warn that they are unused;
that goes away in the next steps.

## 2. Texts

Every visible text is a key in `assets/data/dialogs.csv`, with one column per language
([texts](reference/texts.md)). Append:

```csv
map.attic,Desván,Attic
obj.attic_box,Cajas con tu nombre. No recuerdas haberlas guardado.,Boxes with your name on them. You don't remember packing them.
ev.attic_first,Huele a polvo y a algo más.,It smells of dust and something else.
char.cat,Gato,Cat
conv.cat.hungry,El gato te mira fijamente. Tiene hambre.,The cat stares at you. It is hungry.
conv.cat.fed,El gato come y ronronea.,The cat eats and purrs.
conv.cat.happy,El gato duerme tranquilo.,The cat sleeps peacefully.
opt.cat.feed,Darle de comer (1 moneda),Feed it (1 coin)
opt.cat.leave,Dejarlo,Leave it
hud.coins,Monedas: {v:game.coins},Coins: {v:game.coins}
input.journal,Diario,Journal
journal.title,Diario,Journal
journal.cat_fed,Le diste de comer al gato.,You fed the cat.
journal.cat_hungry,El gato del desván tiene hambre.,The cat in the attic is hungry.
journal.coins,Monedas,Coins
journal.hint,{k:ui.back} cerrar,{k:ui.back} close
```

`{v:game.coins}` prints the variable; `{k:ui.back}` prints whatever key the player bound to *back*.

## 3. A map

A [map](reference/maps.md) is a drawing plus a file that says what its characters mean.

`assets/data/maps/attic.txt`:

```
####################
#..................#
#..b...........b...#
#..................#
#..................#
#..................#
#.........>........#
####################
```

`assets/data/maps/attic_info.json`:

```jsonc
{
  "titleKey": "map.attic",
  "textbox": "map_message",           // the textbox block of scenes/game.json
  "music": "home",
  "spawn": [10, 5],
  "tiles": [
    { "char": "#", "glyph": "█", "fg": "#3b3642", "solid": true },
    { "char": ".", "glyph": "·", "fg": "#2e2e2e" },
    { "char": "b", "glyph": "■", "fg": "#8a6d45", "solid": true, "interact": "obj.attic_box" },
    { "char": ">", "glyph": "▼", "fg": "#c9c9c9" }
  ],
  "exits": [
    { "x": 10, "y": 6, "map": "home", "toX": 5, "toY": 2 }
  ],
  "npcs": [
    { "id": "cat", "x": 10, "y": 3, "glyph": "c", "fg": "#d8c070", "dialog": "cat_talk",
      "variants": [ { "if": "game.fed_cat", "fg": "#78b478" } ] }
  ]
}
```

- `b` boxes are solid and show a text when the player bumps into them or presses interact.
- The `▼` cell at 10,6 takes the player back home.
- The cat turns green once it has been fed: NPC variants read variables.

Connect home to the attic: in `assets/data/maps/home_info.json`, add an exit on a walkable cell:

```jsonc
  "exits": [
    { "x": 5, "y": 1, "map": "attic", "toX": 10, "toY": 5 },
    ...
```

`--validate` checks that exits do not start or land on solid cells or on other exits.

## 4. A conversation

The cat needs a portrait. Portraits are [ASCII images](reference/media.md#images), numbered frames in a
folder per character. `assets/images/characters/cat/1.txt`:

```
 /\_/\
( o.o )
 > ^ <
```

Then the [conversation](reference/conversations.md), in `assets/data/conversations.json`:

```jsonc
  "cat_talk": {
    "character": "cat", "name": "char.cat", "color": "#d8c070", "drawMode": "Inside",
    "nodes": {
      "start": { "image": 1, "branch": [ { "if": "game.fed_cat", "next": "happy" } ], "next": "hungry" },
      "hungry": {
        "text": "conv.cat.hungry",
        "options": [
          { "text": "opt.cat.feed", "lockedIf": "game.coins < 1", "next": "fed",
            "set": { "game.fed_cat": true }, "add": { "game.coins": -1 } },
          { "text": "opt.cat.leave" }
        ]
      },
      "fed": { "text": "conv.cat.fed" },
      "happy": { "text": "conv.cat.happy" }
    }
  },
```

- `start` has no text: it only chooses where to go with a `branch`.
- *Feed it* is visible but locked without coins (`lockedIf`).
- *Leave it* has no `next`, so it ends the conversation.

## 5. An event

[Events](reference/events.md) run actions when something happens. Show a line the first time the
player enters the attic, in `assets/data/events.json`:

```jsonc
  {
    "id": "attic_first_time",
    "trigger": { "type": "enterMap", "map": "attic" },
    "actions": [ { "type": "message", "target": "map_message", "keys": ["ev.attic_first"] } ]
  },
```

Events are `once` by default: after running, they are completed and saved as such.

## 6. Something on screen

Show the coins in the corner of the game. In `assets/data/scenes/game.json`, add a
[`text` block](reference/blocks.md#text) to the root overlay's `children`, before the textbox:

```jsonc
      { "type": "text", "text": "hud.coins", "style": "dim", "x": "right", "y": "top", "offset": [-3, 1] },
```

The block redraws by itself whenever `game.coins` changes, and its width is reserved for the widest
value (`max: 99`) in the widest language.

## 7. A new key and a new screen

**The key.** Every key is an [input action](reference/input.md). Declare `journal` in
`assets/data/input/actions.json`:

```jsonc
  "journal": { "label": "input.journal", "keys": ["J"], "context": "map" }
```

List it on the controls page so players can rebind it, in `assets/data/scenes/controls.json`:

```jsonc
          { "type": "keybind", "action": "journal" },
```

**The screen.** A new modal [scene](reference/scenes.md), `assets/data/scenes/journal.json`:

```jsonc
{
  "presentation": "modal",
  "inputs": {
    "ui.back": [ { "type": "back" } ],
    "journal": [ { "type": "back" } ]
  },
  "root": {
    "type": "stack", "border": true, "padding": [3, 1], "spacing": 1, "background": "background",
    "title": "journal.title", "minWidth": "40%",
    "children": [
      { "type": "branch",
        "cases": [ { "if": "game.fed_cat", "content": { "type": "text", "text": "journal.cat_fed" } } ],
        "else": { "type": "text", "text": "journal.cat_hungry" } },
      { "type": "bar", "var": "game.coins", "max": "10", "label": "journal.coins" },
      { "type": "text", "text": "journal.hint", "style": "hint" }
    ]
  }
}
```

**Connect them.** In `assets/data/scenes/game.json`, make the game scene open the journal:

```jsonc
  "inputs": {
    "pause":   [ { "type": "open", "scene": "pause" } ],
    "journal": [ { "type": "open", "scene": "journal" } ]
  },
```

The map does not handle `journal`, so the action falls through to the scene's `inputs`. In the journal,
the same key closes it.

## 8. Check and play

```bash
dotnet run --project src/Relato -- --validate
#   Text metrics regenerated: assets/data/generated/text_metrics.csv
#   Content OK (146 texts, 24 variables, 9 scenes, 4 maps, 8 conversations, 9 events, 13 input actions)
dotnet run --project src/Relato
```

Start a new game, walk to the top left of your room, meet the cat, and press J.

## Where to go next

- Make the journal reachable from the pause menu too: a `button` with an `open` action.
- Give the cat a second portrait (`2.txt`) and an `imageAfter` in `hungry` so it narrows its eyes while
  the player hesitates; add `"lockAfter": 5` to *Feed it*.
- Use a `step` trigger to make something happen when the player stands in front of the boxes.
- Read the demo's `assets/`: every feature of the engine is used there.

## Starting a game from scratch

To make a different game rather than extend the demo, keep the structure of `assets/` and replace its
content:

1. `data/game.json`: title, grid, languages, start scene, new game scene.
2. `data/variables/*.json`: your state (keep `settings.json`'s bound variables if you keep the settings
   menu).
3. `data/input/actions.json`: keep the [required actions](reference/input.md#required-actions).
4. `data/scenes/`: the engine needs the scenes named in `game.json` (`startScene`, `newGame.scene`,
   `conversationScene`, `cardScene`, and `quitScene` if any). The demo's `dialog.json`, `black.json`,
   `exit.json`, `settings.json` and `controls.json` are good starting points.
5. `dialogs.csv`, maps, conversations, events, images.
6. Run `--validate` until it says `Content OK`.

The C# executable is the same for every game.
