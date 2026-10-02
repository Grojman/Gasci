# `game.json`

`assets/data/game.json` holds everything about the game that is not content. Comments (`//`) are
allowed in every JSON file of the engine.

```jsonc
{
  "title": "Relato",
  "grid": { "minWidth": 100, "minHeight": 36, "fontScales": [2, 1] },
  "font": null,
  "map": { "sectionWidth": 98, "sectionHeight": 34 },
  "images": { "minCharSize": [1.5, 3], "detail": 2 },
  "defaultLanguage": "es",
  "startScene": "title",
  "newGame": { "scene": "game", "params": { "map": "home" } },
  "conversationScene": "dialog",
  "cardScene": "black",
  "quitScene": "exit",
  "persistence": { "delaySeconds": 1.0 },
  "input": { "maxKeys": 2 }
}
```

| Key | Default | Meaning |
|---|---|---|
| `title` | `"Relato"` | Window title. |
| `grid.minWidth`, `grid.minHeight` | `100`, `36` | The smallest grid, in cells, the game is designed for. At least 20×10. Windowed mode uses exactly this size. |
| `grid.fontScales` | `[2, 1]` | Font scales the engine may use (1–4), biggest first. |
| `font` | `null` | `null` for SadConsole's built-in IBM 8×16 font (CP437 layout), or `{ "file": "x.font", "charmap": "x.charmap.txt" }` in `assets/fonts/`. See [fonts](media.md#fonts). |
| `map.sectionWidth`, `map.sectionHeight` | `98`, `34` | Size of a map section: the part of a map shown at once. Independent of the screen, so map design and `halfway` triggers do not depend on the player's monitor. |
| `images.minCharSize` | `[1.5, 3]` | Smallest image character in pixels **at font scale 1** (multiplied by the scale in use: `[1.5, 3]` = 3×6 pixels at scale 2). Its shape is the shape of every image character. |
| `images.detail` | `2` | Image characters per normal cell side for `fit: "none"`. |
| `defaultLanguage` | `"en"` | Column of `dialogs.csv` used when a text is missing in the current language. Must be a column of the file. |
| `startScene` | `"title"` | First scene opened. |
| `newGame.scene`, `newGame.params` | `"game"`, `{}` | Scene and parameters opened by the `newGame` action (after resetting `game.*` variables). |
| `conversationScene` | `"dialog"` | Scene opened by the `dialog` action and by NPC conversations. Receives the parameter `conversation`. |
| `cardScene` | `"black"` | Scene opened by the `black` action. Receives `key`, `image`, `ms`, `big`, `drawMode`, `cursorMode`. |
| `quitScene` | `null` | Scene opened by `quit` before closing (an exit effect). `null` closes at once. |
| `persistence.delaySeconds` | `1.0` | Seconds without changes before `settings.json` and `keybindings.json` are written. |
| `input.maxKeys` | `2` | Keys per input action (primary, secondary...). Also the number of columns of `keybind` controls. |

## Display modes and the grid

The window cannot be resized by hand. The grid is computed at start-up and again only when the display
mode changes (a settings variable bound to `display.mode`; see [variables](variables.md#bindings)):

| Mode | Grid | Window |
|---|---|---|
| `windowed` | Exactly `minWidth × minHeight`. | The grid at the biggest font scale, scaled to 90 % of the screen. |
| `fullscreen` | The minimum grid scaled to fill the screen on its tighter axis; the other axis gets **extra cells**. | Exclusive full screen. |
| `borderless` | Same as fullscreen. | A borderless window covering the screen. |

The font scale is the biggest of `fontScales` that does not shrink the characters below 50 %. On a
1920×1080 screen with the default settings the grid is 128×36 cells.

Because the grid can be bigger than the minimum, **write sizes as percentages, `fill` or `auto`**, never
as fixed cells, unless something really must be a fixed number of cells. `--validate` lays out every
scene at the minimum grid and warns when one does not fit.
