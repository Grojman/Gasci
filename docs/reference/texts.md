# Texts and localisation

Every visible text goes through a **key**. Blocks, maps, conversations and actions never hold literal
text: they name keys of `assets/data/dialogs.csv`.

## `dialogs.csv`

```csv
key,es,en
title.continue,Continuar partida,Continue
title.new,Nueva partida,New game
obj.clock,"El reloj de pared marca siempre las {c:DarkRed}3:17{c}. Las agujas se mueven, pero la hora no cambia.","The wall clock always reads {c:DarkRed}3:17{c}. The hands move, but the time never changes."
```

- The first column is the key; every other column is a **language**, named by its header (`es`, `en`...).
  Adding a language is adding a column.
- Standard CSV (RFC 4180): quote fields that contain commas, quotes (doubled: `""`) or new lines.
- A key missing in the current language falls back to `game.json → defaultLanguage`, then to the key
  itself. `--validate` warns about every missing translation and errors on every missing key.
- The current language is the settings variable bound to `loc.language`, usually a `combobox` with
  `"labelPrefix": "lang."` and the keys `lang.es`, `lang.en`... Changing it re-lays out every scene.
- Key names are free; the demo groups them by prefix (`title.`, `settings.`, `map.`, `obj.`, `ev.`,
  `conv.`, `opt.`, `char.`, `input.`, `key.`).

## Inline codes

Codes inside a text change how it is typed and shown:

| Code | Effect |
|---|---|
| `{c:Red}` / `{c:#ff0000}` | Colour of what follows: a colour name (`DarkRed`, `Wheat`...) or hex. Theme names are not accepted here. `{c}` resets to the block's colour. |
| `{d:80}` | Milliseconds per character from here on. `{d}` resets to the block's `charDelayMs`. |
| `{p:600}` | Pause, in milliseconds, before the next character. |
| `{v:game.player_name}` | The value of a variable. The variable needs `maxLength`, `options` or `min` / `max`, so the width of the text is known. |
| `{k:map.interact}` | The name of the (primary) key bound to an input action. Hints stay right after the player rebinds keys. |

```csv
map.hint,{k:map.up}{k:map.left}{k:map.down}{k:map.right} mover · {k:map.interact} interactuar · {k:pause} menú,{k:map.up}{k:map.left}{k:map.down}{k:map.right} move · {k:map.interact} interact · {k:pause} menu
ev.shadow_appears,Tu sombra se alarga sobre el asfalto...{p:700} {c:#9a4ad0}y se pone de pie.{c},Your shadow stretches over the asphalt...{p:700} {c:#9a4ad0}and stands up.{c}
```

A line break is written `\n` inside a field (or as a real new line in a quoted field). Long texts are word-wrapped to
their block; a `textbox` splits them into pages.

### Key names

`{k:}` and `keybind` controls print key names through the text keys `key.<Name>` when they exist
(`key.Space` → "Espacio", `key.Up` → "↑"), and a short default otherwise (`Esc`, `Space`, `Backspace`...).

## Text metrics

Layout needs to know how big a text is before it is shown, in every language. `--validate` writes
`assets/data/generated/text_metrics.csv`:

| Column | Meaning |
|---|---|
| `natural` | Width of the longest line with nothing wrapped (codes removed). |
| `min` | Width of the longest word: the narrowest the text can wrap to. |
| `lines` | Number of lines with nothing wrapped. |

Blocks use the widest value **across languages**, so a menu does not change size when the player
switches language. The file starts with the SHA-256 of `dialogs.csv`; if they do not match, the game
measures at start-up and logs a warning. Commit the regenerated file with every `dialogs.csv` change.
See [validation](../validation.md#text-metrics).

## Characters and fonts

Every character of every text must exist in the font. With the built-in font that is the IBM CP437 set
(accented Latin letters, box drawing, shades, arrows...). `--validate` warns about any character the
font cannot draw. See [fonts](media.md#fonts).
