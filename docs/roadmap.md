# Roadmap and limitations

## Planned

- **Separate engine and game.** Today the engine and the demo share one project and the assets are
  copied from the repository root. The goal is an engine library plus a thin launcher that can point at
  any game folder.
- **Several players.** Today one map block per scene has `"player": true`, and `teleport` targets it.
  The plan is to let actions and events target a specific player or map block, so several maps can be
  controlled at the same time. One use is a minigame where one player moves in one map and another in a
  second map.
- **Collections** (array variables) and a `list` block that repeats a template over them, for
  inventories.
- **A TTF → SadConsole font converter**, as a separate tool, so any TrueType font can be used. SadConsole
  only loads bitmap tile fonts.
- **A message log block** (backlog of past messages).

## Known limitations

| Limitation | Cause | Workaround / status |
|---|---|---|
| No mouse | Design decision: keyboard-only terminal feel | — |
| No drop-down combo box | Design decision | Combo boxes cycle `◄ value ►`. |
| Only bitmap tile fonts | SadConsole renders tile sheets | Custom `.font` files are supported; TTF needs an external converter. |
| Characters limited to the font's character map | One glyph per cell | `--validate` reports texts and maps using missing characters. |
| Images cannot shrink below the minimum character size | Art is never down-sampled | The image is cut and a warning is logged. |
| No arrays in variables | Not in scope for 1.0 | Planned (collections). |
| Integer-only maths | Design decision | Scale values (e.g. store tenths). |
| One player map | Not in scope for 1.0 | Planned (several players). |
| A scene can be only once in the stack | Every block id is unique in the whole game | Use separate scenes. |

## Needs a manual check

These parts depend on SadConsole/MonoGame at runtime and are not covered by unit tests. Check them by
playing after changing the code around them:

1. Switching display modes (windowed ↔ fullscreen ↔ borderless) and the re-layout that follows.
2. Rebinding keys on the controls page, including swapping and cancelling with Enter.
3. Save → continue (scene stack, player position, events) and autosave.
4. A custom font (none is shipped; the check is covered by the validator only).
