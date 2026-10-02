# Blocks

A scene is a tree of blocks. Each block is a JSON object whose `type` picks what it is. This page lists
every block and control with all its properties; defaults are in the second column.

- [Common properties](#common-properties)
- Containers: [`stack`](#stack), [`overlay`](#overlay), [`menu`](#menu), [`branch`](#branch)
- Text: [`text`](#text), [`textbox`](#textbox)
- Media and game: [`image`](#image), [`map`](#map), [`conversation`](#conversation), [`choices`](#choices), [`card`](#card)
- Others: [`bar`](#bar), [`effect`](#effect), [`timer`](#timer), [`spacer`](#spacer)
- Controls: [`button`](#button), [`switch`](#switch), [`slider`](#slider), [`combobox`](#combobox), [`numberfield`](#numberfield), [`textfield`](#textfield), [`keybind`](#keybind)

Text properties (`text`, `label`, `title`, `hint`...) take **text keys** from [`dialogs.csv`](texts.md),
never literal text. Colours take [theme](media.md#theme) names or literal colours. Conditions are
[expressions](expressions.md).

## Common properties

Every block accepts these.

| Property | Default | Meaning |
|---|---|---|
| `type` | — | The block type. |
| `id` | — | Unique in the **whole game** (not only in the scene). Needed to target the block from actions, events, maps or focus changes. |
| `width`, `height` | `"auto"` | `"auto"`, `"fill"`, `"40%"` or cells. See [layout](scenes.md#layout). |
| `minWidth`, `maxWidth`, `minHeight`, `maxHeight` | — | Limits, same units. |
| `x`, `y` | parent's | Anchors: `left` / `center` / `right`, `top` / `center` / `bottom`. |
| `offset` | `[0, 0]` | Cells to move the block after anchoring. |
| `visibleIf` | — | Condition: the block is shown only while it is true. |
| `enabledIf` | — | Condition: the block is enabled only while it is true (disabled controls are skipped by menus and drawn dimmed). |
| `visible` | `true` | Initial visibility (the engine shows and hides some blocks, e.g. message boxes). |
| `style` | — | Colour of the block's content. |
| `background` | — | Background colour of the block's area. An opaque background hides what is below. |
| `focus` | `false` | Focused when the scene opens. |
| `backdrop` | `none` | `dim` / `opaque`: a layer drawn under the block over the whole scene (popups inside a scene). |
| `inputs` | — | `{ "action": [actions] }`: what the block does with input actions while it is focused. |
| `raiseEvents` | `false` | The block reports its input (`input` trigger) and changes (`change` trigger) to the [event engine](events.md), and lets events run while it is focused and idle. |
| `passInput` | `false` | React to input without consuming it, so lower focused blocks also receive it. |

---

## Containers

`stack` and `overlay` share:

| Property | Default | Meaning |
|---|---|---|
| `children` | `[]` | The child blocks. |
| `border` | `false` | Draw a frame around the container. |
| `padding` | `[0, 0]` | Space between the border and the children: `[horizontal, vertical]`, or one number for both. |
| `title` | — | Text key printed on the top border. |
| `borderColor` | theme `border` | Colour of the frame. |

### `stack`

Children flow one after another. A child's `x` / `y` anchor only applies on the cross axis.
Percentage children share the main axis, carrying the rounding remainder to the next one.

| Property | Default | Meaning |
|---|---|---|
| `direction` | `"vertical"` | `vertical` or `horizontal`. |
| `spacing` | `0` | Cells between children. |
| `align` | `center` | Default cross-axis anchor of the children. |
| `justify` | `start` | Where the children go on the main axis when they do not fill it. |

### `overlay`

Children are placed by their own anchors and may overlap; later children are drawn on top. The usual
root of a full-screen scene.

### `menu`

A `stack` of [controls](#controls) with a selection. Up / down (`ui.up` / `ui.down`; left / right with
`"navigation": "horizontal"`) move the selection over the visible, enabled controls; every other action
goes to the selected control. Labels of value controls are aligned in one column. The selection is kept
when the scene is resumed.

| Property | Default | Meaning |
|---|---|---|
| `navigation` | `"vertical"` | `vertical` or `horizontal`. |
| *(stack properties)* | | `children`, `spacing`, `align`, `border`... |

### `branch`

Shows content depending on conditions.

```jsonc
{ "type": "branch", "cases": [
    { "if": "game.health <= 0", "content": { "type": "text", "text": "hud.dead" } },
    { "if": "game.health < 3",  "content": { "type": "text", "text": "hud.hurt", "style": "highlight" } }
  ],
  "else": { "type": "text", "text": "hud.fine" } }
```

| Property | Default | Meaning |
|---|---|---|
| `cases` | `[]` | `[{ "if": condition, "content": block }]`. |
| `else` | — | Block shown when no case is true. |
| `mode` | `"first"` | `first`: only the first true case. `all`: every true case, stacked in order. |
| `spacing` | `0` | Cells between cases in `all` mode. |

---

## Text

### `text`

A static text: a text key, or the value of a variable. Measured with the widest translation (and the
widest value the variable can hold), so it keeps its size when the language or the value changes.

| Property | Default | Meaning |
|---|---|---|
| `text` | — | Text key. |
| `textVar` | — | A variable to show instead. Translated when its value is a text key (e.g. `session.pause_status` holding `"pause.saved"`). |
| `scale` | `1` | Font scale: `2`, `3`... for big text. |
| `align` | `center` | Alignment of the lines inside the block. |
| `wrap` | `true` | Wrap lines to the block's width. |

### `textbox`

Typewriter text with pages: `▼` when there is more, `►` at the end. Used for map messages, dialog lines
and cards. It is the target of the `message` [action](actions.md) and of map interaction texts.

While it shows a queue of messages it takes the scene's focus (`move`) and gives it back (`release`)
when the queue ends, which is why the map stops walking while a message is open. `ui.confirm` finishes
the page being typed, or moves to the next.

| Property | Default | Meaning |
|---|---|---|
| `text` | — | Text key shown when the scene opens (a static box). |
| `autoHide` | `true` | Hidden while it has nothing to show. |
| `border` | `true` | Draw a frame. |
| `scale` | `1` | Font scale. |
| `centerLines` | `false` | Centre each line. |
| `charDelayMs` | `22` | Milliseconds per character (inline `{d:}` codes override it). |
| `indicateEnd` | `true` | Show `▼` / `►`. |
| `typeSound` | `true` | Play a sound while typing. |
| `takeFocus` | `true` | Take the focus while showing a queue. Composite blocks that drive the box set it to `false`. |

---

## Media and game blocks

### `image`

An ASCII image from `assets/images/`, drawn by an animated cursor and fitted to the block like CSS
`object-fit`. See [images](media.md#images).

| Property | Default | Meaning |
|---|---|---|
| `image` | — | Image key (`"general/title"` for `images/general/title.txt`) or a parameter (`"$image"`). Empty shows nothing. |
| `fit` | `"contain"` | `contain`, `cover`, `fill`, `none`, `scale-down`. |
| `position` | `"center"` | Where the image sits: `"center"`, `"top"`, `"bottom left"`... |
| `drawMode` | `"Random"` | Order in which cells are drawn: `Random`, `Borders`, `Inside`, `TopToBottom`, `ZigZag`. |
| `cursorMode` | `"Multiple"` | `Multiple` (many cells written at once) or `Single` (one cursor travels). |
| `color` | — | Colour of the drawing. |
| `duration` | — | Seconds the whole drawing takes (wins over `speed`). |
| `speed` | `700` | Normal cells drawn per second. |
| `skippable` | `true` | `ui.confirm` / `ui.back` complete the drawing at once, whatever has the focus. |
| `detail` | `images.detail` | Image characters per cell side for `fit: "none"`. |

### `map`

A viewport on a tile [map](maps.md). It shows one section at a time with a frame whose arrows show in
which directions there is more map; walking off a section loads the next one. If the block is smaller
than a section, the map is drawn with smaller characters so the section still fits.

| Property | Default | Meaning |
|---|---|---|
| `map` | — | Map id or a parameter (`"$map"`). |
| `startX`, `startY` | map spawn | Start position (global map cells) or parameters. |
| `player` | `false` | This block owns the player: moves with `map.*` actions, interacts, is saved, receives `teleport`. Only one per scene. |
| `hint` | — | Text key printed on the bottom frame (use `{k:}` codes for keys). |
| `showTitle` | `true` | Print the map's `titleKey` on the top frame. |

With `"raiseEvents": true` the map fires the `enterMap`, `step` and `halfway` [triggers](events.md).
**The map has no message box of its own**: interaction texts go to the `textbox` named in the map info.

### `conversation`

Plays a [conversation](conversations.md). It drives three slot blocks, each laid out like any other
block: `portrait` (an `image`), `text` (a `textbox`) and `options` (a `choices`).

```jsonc
{ "type": "conversation", "id": "conversation", "conversation": "$conversation", "focus": true,
  "width": "fill", "height": "fill",
  "portrait": { "width": "fill", "height": "fill", "fit": "contain" },
  "text":     { "width": "fill", "height": 5, "y": "bottom", "offset": [0, -3], "background": "background" },
  "options":  { "width": "fill", "height": 4, "y": "bottom", "offset": [0, 1],  "background": "background" } }
```

| Property | Default | Meaning |
|---|---|---|
| `conversation` | — | Conversation id or a parameter. |
| `portrait` | `{}` | `image` properties of the portrait slot. |
| `text` | `{}` | `textbox` properties of the text slot. |
| `options` | `{}` | `choices` properties of the options slot. |
| `onEnd` | `[back]` | Actions run when the conversation ends. |

### `choices`

A list of options with locked options and an optional countdown. Used by `conversation`; also usable on
its own for prompts.

| Property | Default | Meaning |
|---|---|---|
| `items` | — | Standalone options: `[{ "text": key, "if": condition, "lockedIf": condition, "actions": [...] }]`. `if` hides the option; `lockedIf` shows it but it cannot be chosen. |

### `card`

The black screen of the `black` [action](actions.md): an image and / or a line of text, closed after a
time or when the player confirms. It reads the card scene's parameters (`key`, `image`, `ms`, `big`,
`drawMode`, `cursorMode`).

| Property | Default | Meaning |
|---|---|---|
| `image` | `{}` | `image` properties of the image slot. |
| `text` | `{}` | `textbox` properties of the text shown **with** an image. |
| `soloText` | `{}` | `textbox` properties of the text shown **without** an image. |
| `onEnd` | `[back]` | Actions run when the card closes. |

---

## Other blocks

### `bar`

A read-only gauge bound to an int variable: `████░░░░`.

| Property | Default | Meaning |
|---|---|---|
| `var` | — | Int variable. |
| `max` | variable's `max` | A number or an int expression (`"game.max_health"`). |
| `min` | variable's `min` | A number or an int expression. |
| `full`, `empty` | `"█"`, `"░"` | Characters of the filled and empty parts. |
| `emptyStyle` | — | Colour of the empty part. |
| `label` | — | Text key printed before the bar. |

### `effect`

A procedural effect over the block's area.

| Property | Default | Meaning |
|---|---|---|
| `effect` | `"dissolve"` | `dissolve`: the area fills with random bits until covered, fades to black, then runs `onDone` (stops the music). `glitch`: random shade characters flicker. |
| `speed` | `2600` | Speed of the dissolve. |
| `intensity` | — | `glitch` only: an int expression; how many characters flicker (0 = none). E.g. `"game.distortion"`. |
| `onDone` | — | `dissolve` only: actions run when covered. |

### `timer`

Invisible. Runs actions after a time.

| Property | Default | Meaning |
|---|---|---|
| `seconds` | `0` | Delay. |
| `when` | — | Condition: the delay counts while it is true, and starts again each time it becomes true. Without it, the delay starts when the scene opens. |
| `repeat` | `false` | Run every `seconds`. |
| `actions` | `[]` | Actions to run. |

```jsonc
// Clear a status message 2.5 s after it appears.
{ "type": "timer", "when": "session.pause_status != ''", "seconds": 2.5,
  "actions": [ { "type": "set", "var": "session.pause_status", "value": "" } ] }
```

### `spacer`

Empty space; give it a `width` / `height`.

---

## Controls

Controls live inside a [`menu`](#menu). Every control has a `label` (text key). Value controls also have:

| Property | Default | Meaning |
|---|---|---|
| `var` | — | The variable the control edits. Its type must match the control. |
| `onChange` | — | Actions run after the value changed (a sound, another variable...). |

The theme gives the default sounds (`move`, `confirm`, `locked`, `change`), so menus sound right without
`sfx` actions. Controls keep the variable inside its [rules](variables.md#rules) on screen.

### `button`

Runs `actions` on `ui.confirm`.

```jsonc
{ "type": "button", "label": "title.continue", "enabledIf": "system.save_exists", "actions": [ { "type": "continue" } ] }
```

### `switch`

A bool variable: `◄ On ►`. `ui.confirm`, `ui.left` and `ui.right` flip it.

| Property | Default | Meaning |
|---|---|---|
| `onText`, `offText` | `"ui.on"`, `"ui.off"` | Text keys of the two states. |

### `slider`

An int variable between `min` and `max`: `◄ ███░░ ►`. `ui.left` / `ui.right` change it by `step` and
stop at the limits.

| Property | Default | Meaning |
|---|---|---|
| `min`, `max` | variable's | Limits. |
| `step` | `1` | Change per press. |

### `combobox`

Cycles through values: `◄ English ►` with `ui.left` / `ui.right` (there is no drop-down).

| Property | Default | Meaning |
|---|---|---|
| `options` | — | `[{ "value": ..., "label": key }]`. |
| `labelPrefix` | — | Without `options`, the variable's own `options` are used, labelled with the text key `labelPrefix + value` (`"lang."` + `"en"` → `lang.en`). |

When the settings file has no value yet, the template default is shown.

### `numberfield`

An int variable: `◄ 12 ►`. `ui.left` / `ui.right` change it by `step`; `ui.confirm` starts typing
digits. Digits that would leave the variable's range are refused.

| Property | Default | Meaning |
|---|---|---|
| `step` | `1` | Change per press. |

### `textfield`

A string variable. `ui.confirm` starts editing: typed characters are added until the variable's
`maxLength` (then refused with the locked sound; a counter shows the limit). Enter confirms, Escape
cancels. Only characters the font can draw are accepted.

### `keybind`

The keys of an input action, one column per key slot (`input.maxKeys`). Left / right choose the slot;
Enter starts listening and the next key pressed is bound to the slot; Enter again cancels. A key already
used by another action of the same context is swapped. See [input](input.md#rebinding).

| Property | Default | Meaning |
|---|---|---|
| `action` | — | Input action id (`"map.up"`). |
