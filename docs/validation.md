# Validation

```bash
dotnet run --project src/Relato -- --validate
```

`--validate` loads every asset without opening a window, resolves every reference between them, lays
out every scene at the minimum grid, and prints what it finds:

```
warning: variable game.gold is declared but never used; remove it
error:   map 'street' exit 195,7: unknown map 'parque'
1 error(s)
```

It exits with **0** when there are no errors and **1** otherwise, so it can run in a script or CI
before a build. Warnings never change the exit code.

It is slow on purpose: it is a safeguard to run before building the game, not part of the game.

## What it checks

Content is loaded in the same order as the game (`game.json`, theme, font, texts, variables, input,
images, maps, conversations, events, scenes). Errors in the files compiled while loading (variable
templates, event conditions, scene conditions) stop the load at the first error, with
`FAILED to load content: ...`. Everything after loading is collected and listed together.

| Area | Errors | Warnings |
|---|---|---|
| **`game.json`** | Minimum grid below 20×10; empty or unsupported `fontScales` (1–4); invalid `images.minCharSize`; unknown start / new game / conversation / card / quit scene, or wrong scene parameters. | No scene has `"saved": true` (the game can never be saved); an engine binding has no settings variable (the player cannot change it). |
| **Font** | Missing or invalid tile sheet, glyphs smaller than 4×6, fewer than 96 glyphs, a character the engine draws missing from the character map. | — |
| **Texts** | `{v:}` on a variable without a known width; references to missing keys from anywhere. | A key without a translation in some language; a character the font cannot draw. |
| **Variables** | Undeclared variables anywhere; writes to read-only variables; `add` on non-int variables; `value` of the wrong type. | A `set` value that breaks the variable's rules; a declared variable nothing uses. |
| **Expressions** | Syntax errors, unknown variables or functions, type mismatches, `rand` in a condition. | — |
| **Actions** | Unknown types; missing `scene`, `target`, `id`; scenes, blocks, conversations, images, maps or input actions that do not exist; unknown focus modes and draw modes; a `message` target that is not a textbox; a focus target in another scene. | — |
| **Maps** | Unreadable drawings; variants of a different size; tile chars longer than one character; a solid spawn; solid exits, exits to unknown maps, to solid cells or to other exits; NPCs out of bounds or with unknown conversations; interaction texts with no textbox. | Characters the font cannot draw. |
| **Conversations** | Unknown draw / cursor modes; missing start node; `next` / branch targets that do not exist; missing portrait frames; wrong types in `set` / `add`. | — |
| **Events** | Duplicated ids; unknown triggers; map triggers without a map or with an unknown map; `input` / `change` blocks that do not exist or lack `raiseEvents`; unknown input actions. | `once: false` on an immediate event; no block has `raiseEvents` (no event could ever run). |
| **Scenes** | Unknown input actions in `inputs`; more than one `"player": true` map per scene; invalid colours; images and maps that do not exist; layout errors. | A scene that needs more cells than the minimum grid (part of it would be cut on small screens). |

## Text metrics

Last, `--validate` regenerates `assets/data/generated/text_metrics.csv`: per text key and language, the
width of the widest line, the width of the longest word and the number of lines. Its first line holds
the SHA-256 of `dialogs.csv`. The file is written both to the source `assets/` (found by walking up from
the executable) and to the build copy, and only when the hash changed.

Blocks measure texts with the widest translation, so a menu keeps its size when the language changes.
When the stored hash does not match the current `dialogs.csv`, the game computes the metrics at
start-up instead and logs a warning: it still works, but run `--validate` and commit the regenerated
file.

**Do not edit `text_metrics.csv` by hand.** Commit it with the `dialogs.csv` change that produced it.
