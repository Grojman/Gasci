# Scenes

Every screen of the game is a **scene**: a JSON file in `assets/data/scenes/<id>.json` holding a tree
of [blocks](blocks.md). The file name is the scene id.

```jsonc
// scenes/game.json
{
  "params": { "map": "string" },          // given by whoever opens the scene; blocks read them as "$map"
  "presentation": "fill",                 // "fill", "modal", or an object (below)
  "music": "title",                       // missing: keep the current music; "": stop it
  "saved": true,                          // part of the game state: stored in the save with its blocks
  "inputs": { "pause": [ { "type": "open", "scene": "pause" } ] },   // actions no focused block consumed
  "onEnter": [], "onResume": [], "onLeave": [],
  "root": { "type": "overlay", "width": "fill", "height": "fill", "children": [ ... ] }
}
```

| Property | Default | Meaning |
|---|---|---|
| `params` | `{}` | Parameters the scene needs, with their type (`int`, `bool`, `string`). Any block property that takes an id or value can read one as `"$name"`. Whoever opens the scene must pass all of them with the right type (checked by `--validate`). |
| `presentation` | `"fill"` | Where the root goes and how the scenes below are shown (below). |
| `music` | keep | Music id played when the scene opens. Missing keeps the current music; `""` stops it. |
| `saved` | `false` | The scene is part of the game state: `save` stores it with its parameters and its blocks' state, and `continue` restores it. Menus are not saved scenes, so saving from the pause menu restores into the game. |
| `inputs` | — | `{ "action": [actions] }`: handlers for input actions no focused block consumed. |
| `onEnter` | — | Actions run when the scene is opened. |
| `onResume` | — | Actions run when the scene becomes the top again (a scene above it closed). |
| `onLeave` | — | Actions run when the scene is closed. |
| `root` | — | The root block. |

## The scene stack

Scenes are opened on a stack with the `open`, `goto`, `back` and `closeAll` [actions](actions.md).

- **Only the top scene is updated and receives input.** The scenes below are frozen: the map stops
  under the pause menu, timers do not progress behind a menu.
- A scene below is **drawn** only if the scenes above let it show through (their backdrop).
- **Every scene is created once, when the game loads, and lives as long as the game.** That is why block
  ids are unique in the whole game: any block can be addressed from anywhere.
- So **a scene can be only once in the stack.** Opening it again re-applies its parameters and resets its
  blocks.

## Presentation

| Value | Meaning |
|---|---|
| `"fill"` | The whole screen, opaque. Hides the scenes below (which are then not drawn at all). |
| `"modal"` | Sized to its content (`auto`), centred, over a dimmed copy of the scene below. |
| `{ ... }` | `width`, `height` (sizes), `x`, `y` (anchors), `backdrop` and `preset` (`fill` or `modal`, to start from and override). |

`backdrop` is how the scene below is shown: `none` (drawn as it is), `dim` (under a translucent layer)
or `opaque` (not drawn).

```jsonc
"presentation": "modal"
"presentation": { "backdrop": "none" }                               // fill, but the scene below shows through
"presentation": { "preset": "modal", "backdrop": "opaque", "y": "top" }
```

Modal has no behaviour of its own: since only the top scene is enabled, any scene above another one
already blocks input and freezes what is below.

## Layout

Every block has a size and a position in its parent. Sizes are written as:

| Value | Meaning |
|---|---|
| `"auto"` (default) | Fit the content: the block's measured size. |
| `"fill"` | All the space the parent gives (= `"100%"`). |
| `"40%"` | 40 % of the space the parent gives (of the screen for the scene root). |
| `12` | 12 cells. Allowed, but discouraged: the grid size depends on the screen. |

| Property | Meaning |
|---|---|
| `width`, `height` | The size, as above. |
| `minWidth`, `maxWidth`, `minHeight`, `maxHeight` | Limits, with the same units. |
| `x` | Horizontal anchor in the parent: `left`, `center`, `right` (also `start` / `end`). |
| `y` | Vertical anchor in the parent: `top`, `center`, `bottom` (also `start` / `end`, `middle`). |
| `offset` | `[x, y]` cells to move the block after anchoring. |

- In a **`stack`**, children flow along the main axis, and a child's anchor only applies on the cross
  axis (`x` in a vertical stack). `align` sets the default cross-axis anchor and `justify` where the
  children go on the main axis when they do not fill it.
- In an **`overlay`**, children are placed by their own anchors and may overlap. Later children are
  drawn on top.
- **Percentages** of one container share its cells by carrying the rounding remainder to the next
  element (three 33 % children of 100 cells get 33, 33, 34). When they add up to 100 %, the last one
  absorbs what is left, so they always fill the container exactly.
- **Texts** are measured with their widest translation (see [texts](texts.md#text-metrics)), so a menu
  keeps its size when the language changes.

Because the grid can be bigger than `game.json`'s minimum (fullscreen adds cells), layouts written in
percentages, `fill` and `auto` adapt to every screen; fixed cells leave the extra space unused.
`--validate` lays out every scene at the minimum grid and warns about anything that does not fit.

## Focus

The **focused** blocks of the top scene receive the input actions.

- When a scene opens, the blocks with `"focus": true` are focused. A container passes focus to its
  focused child (a `menu` to its selected control).
- Focused blocks receive each action **topmost first**. A block that handles an action **consumes** it,
  unless it has `"passInput": true`. What no block consumes goes to the scene's `inputs`.
- Hidden or disabled blocks lose focus.
- Each `menu` remembers its selected control, so returning from *Settings* keeps *Settings* selected.

The `focus` [action](actions.md#focus) changes focus with a `mode`:

| Mode | Origin (the block running the action) | Target | Example |
|---|---|---|---|
| `move` (default) | loses focus | gains focus | A message box opens: the map stops walking. |
| `share` | keeps focus | gains focus | A hotbar listens to its keys while the map keeps walking. |
| `release` | loses focus | — | The message box closes. |

`move` remembers the origin. When a `release` leaves the scene with nothing focused, the remembered
blocks get focus back (the message closes → the map gets focus again); if nothing is remembered, the
blocks with `"focus": true` get it.

## Popups inside a scene

A block with `"backdrop": "dim"` (or `"opaque"`) draws a layer under itself over the whole scene. Combined
with `visibleIf` and focus changes, it makes a popup without a new scene.

## Rendering

- A block entirely covered by an opaque block above it is not drawn. Scenes hidden behind an opaque
  scene are not drawn either.
- Texts, menus, controls and gauges redraw only when what they show changed. The map and effects are
  animated, so they redraw every frame.
- Colours (`style`, `background`, `borderColor`...) are [theme](media.md#theme) colour names or
  literal colours (`"#ff0000"`, `"DarkRed"`).

## The demo's scenes

| Scene | Shows |
|---|---|
| `title.json` | An overlay with the title image (skippable draw animation), a menu and a hint. |
| `game.json` | The player map, a glitch effect bound to `game.distortion`, and the textbox for map messages. Saved. |
| `pause.json` | A full-screen menu with a save status message cleared by a timer. |
| `settings.json` | A modal menu with sliders, combo boxes, a switch and a number field bound to settings variables. |
| `controls.json` | A modal page of `keybind` controls and a reset button. |
| `dialog.json` | The conversation scene (`game.json → conversationScene`). |
| `black.json` | The card scene of the `black` action (`game.json → cardScene`). |
| `exit.json` | The quit effect: a `dissolve` over the scene below, then `exit`. |
