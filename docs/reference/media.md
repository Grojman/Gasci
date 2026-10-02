# Images, theme, fonts and audio

## Images

ASCII images are UTF-8 text files under `assets/images/`. Their **key** is the path without the
extension: `images/general/title.txt` → `"general/title"`, `images/characters/mother/2.txt` →
`"characters/mother/2"`.

```
assets/images/
├── general/            title.txt, eye.txt...
└── characters/
    └── mother/         1.txt, 2.txt — numbered portrait frames of a conversation character
```

The file only holds the **drawing**. How it is shown is decided by the [`image` block](blocks.md#image)
(and the `card` / `conversation` slots), with the same terms as CSS `object-fit` / `object-position`:

| `fit` | Behaviour |
|---|---|
| `contain` (default) | The biggest image character that shows the whole image keeping its proportions. Letterboxed. |
| `cover` | The smallest character that fills the whole block keeping proportions; what overflows is cut. |
| `fill` | Characters stretched independently in X and Y to fill the block exactly (the drawing is deformed). |
| `none` | Characters at `detail` image characters per cell side; what does not fit is cut. |
| `scale-down` | `none` if the image fits, otherwise `contain`. |

`position` (`"center"`, `"top"`, `"bottom left"`...) places the image inside the block, and chooses which
part is cut for `cover` and `none`.

Images are drawn with **smaller characters** than the normal grid, so they are much more detailed than
the text around them. A drawing is never down-sampled: an image is shrunk only by using smaller
characters, down to `game.json → images.minCharSize`. An image that would need smaller characters is
cut, and a warning is logged.

Shade characters are coloured darker than solid ones (`░` at 45 %, `▒` at 65 % of the colour), which
gives the horror-ASCII look with a single colour.

### Draw animation

Images are drawn by a cursor, character by character.

| `drawMode` | Order |
|---|---|
| `Random` | Random cells, no order. |
| `Borders` | From the edges of the area towards the centre. |
| `Inside` | From the centre outwards. |
| `TopToBottom` | Line by line, top to bottom. |
| `ZigZag` | Alternates one random cell of the top half with one of the bottom half. |

| `cursorMode` | Behaviour |
|---|---|
| `Multiple` | Several cells are being written at the same time, each with its own cursor. |
| `Single` | One cursor travels through every cell; a cell is written as soon as the cursor moves on. |

`duration` (seconds for the whole drawing) or `speed` (cells per second) set the pace. Replacing an
image with another of the same size redraws only the cells that differ, which is how portraits change
expression.

## Theme

`assets/data/ui/theme.json` names colours and the default UI sounds.

```jsonc
{
  "colors": {
    "text": "#c8c8c8", "dim": "#6e6e6e", "hint": "#505050", "highlight": "#e63c3c",
    "border": "#5a5a64", "disabled": "#3c3c3c", "ok": "#78b478", "background": "#000000",
    "title": "#aa141e", "card": "#961419"
  },
  "sounds": { "move": "select", "confirm": "confirm", "locked": "locked", "change": "select" }
}
```

- **Colours** can be used wherever a block expects a colour (`style`, `background`, `borderColor`,
  `color`, `emptyStyle`...). Add any name you like. The engine itself uses `text` (the default
  colour), `dim`, `highlight`, `border` and `disabled`; keep those defined.
- Everywhere a colour is expected, a literal also works: hex (`"#ff0000"`) or a colour name (`"DarkRed"`,
  `"Wheat"`...). Inline `{c:}` codes in texts accept literals only.
- **Sounds**: `move` (selection moves in a menu), `confirm` (a button or option is chosen), `locked`
  (a disabled or locked item is chosen, a text field is full), `change` (a value control changes).

## Fonts

By default the engine uses SadConsole's built-in IBM 8×16 font with the CP437 character set. A custom
font is set in `game.json`:

```jsonc
"font": { "file": "myfont.font", "charmap": "myfont.charmap.txt" }
```

Both files go in `assets/fonts/`:

- **`.font`**: a SadConsole bitmap font: a JSON file that points to a **PNG tile sheet**. TrueType fonts
  are not supported (a converter is on the [roadmap](../roadmap.md)).
- **Character map**: a UTF-8 text file with one line per row of the tile sheet. The *n*-th character of
  the file (new lines excluded) is drawn with glyph *n*. So the engine does not depend on the IBM
  layout.

The font is checked by `--validate` and again at start-up: the tile sheet must exist and be a PNG,
glyphs must be at least 4×6 pixels, there must be at least 96 glyphs, and the map must include every
character the engine draws (frames, cursors, arrows, bars). Texts, maps and images using characters the
font lacks are reported.

## Audio

| Kind | Folder | Behaviour |
|---|---|---|
| Music | `assets/audio/music/<id>.wav` | Long looping tracks or ambiences. One at a time. |
| Sound effects | `assets/audio/sfx/<id>.wav` | Short sounds. |

**Every id has a synthesized placeholder** built into the engine, so a game makes sound before it has
any audio file. A `.wav` with the same id replaces the placeholder.

| Built-in music | Built-in sound effects |
|---|---|
| `title`, `home`, `street`, `park`, `shadow` | `cursor`, `step`, `select`, `confirm`, `locked`, `page`, `save`, `door`, `creature` |

- Music does not depend on who plays it. A map plays its `music` when it loads; a scene with `music`
  plays it when it opens; a conversation with `music` plays it and restores the previous one at the end;
  the `music` action plays or stops it.
- To stop the music, use a `music` action with `""` or open a scene with `"music": ""`.
- Volumes come from the settings variables bound to `audio.music` and `audio.sfx`.
- Without an audio device the game runs silently.
