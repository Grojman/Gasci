# Getting started

## Requirements

- The [.NET 9 SDK](https://dotnet.microsoft.com/download).
- Linux, Windows or macOS with OpenGL (MonoGame DesktopGL). The NuGet packages (`SadConsole.Host.MonoGame`
  10.10.1, `MonoGame.Framework.DesktopGL` 3.8.5.1) are restored automatically on the first build.
- A keyboard. The engine does not use the mouse.

## Running

```bash
dotnet run --project src/Gasci                 # play
dotnet run --project src/Gasci -- --validate   # check every asset without opening a window
dotnet test                                     # unit tests of the engine logic
```

The build copies the whole `assets/` folder next to the executable (`bin/<config>/net9.0/assets`), so the
game always runs with the assets of the source tree it was built from. Edit files in the root
`assets/`, never in `bin/`.

### Default controls

| Action | Keys |
|---|---|
| Move (map), navigate (menus) | Arrows, W A S D |
| Interact (map) | E, Space |
| Accept (menus, texts) | Enter (fixed), Space, E |
| Back / pause | Esc |

All of them except Enter can be changed in game in *Settings → Controls*, and the defaults are data in
[`input/actions.json`](reference/input.md).

## The workflow

1. Edit the files in `assets/` (see the [content reference](../README.md#documentation)).
2. Run `--validate`. It reports every broken reference, type error and layout problem with the file and
   the place where it happens, and regenerates `assets/data/generated/text_metrics.csv`. See
   [Validation](validation.md).
3. Run the game.

Content errors that `--validate` would catch also stop the game at start-up (with the error in the
log), so a broken asset never produces a half-working game. `--validate` simply finds **all** of them at
once, and also checks things the game only notices when it reaches them.

## Testing

`tests/Gasci.Tests` holds xUnit tests for the pure-logic parts of the engine:

| File | Covers |
|---|---|
| `ExpressionTests.cs` | Lexer, parser, type checking, operators, functions, runtime errors. |
| `LayoutTests.cs` | Sizes, anchors, percentage distribution, text wrapping and inline codes. |
| `VariableTests.cs` | Templates, rules, clamping, rejected writes, loading values. |
| `InputTests.cs` | Defaults, overrides, conflicts, swapping, reset. |
| `EventTests.cs` | Trigger indexing, conditions, `once`, immediate events and dependencies. |
| `PathsTests.cs` | Game titles turned into player folder names. |

`TestContent.cs` writes small variable templates to a temporary folder, so the tests do not depend on the
demo's assets.

```bash
dotnet test                                   # everything
dotnet test --filter "FullyQualifiedName~ExpressionTests"
```

Anything that needs SadConsole running (drawing, display modes, audio) is not unit-tested; check it by
playing.

## Publishing a build

```bash
dotnet publish src/Gasci -c Release -r linux-x64 --self-contained   # or win-x64, osx-arm64...
```

The output folder (`src/Gasci/bin/Release/net9.0/<rid>/publish/`) contains the executable and the
`assets/` folder. Ship the whole folder. Run `--validate` before publishing.

## Player files and the log

Each game keeps its files in a folder named after its `title` in [`game.json`](reference/game-config.md),
so several games made with Gasci never share saves or settings. For the demo (title `Relato`):
`~/.local/share/Relato` (Linux), `%LOCALAPPDATA%\Relato` (Windows) or
`~/Library/Application Support/Relato` (macOS). Characters that are not valid in folder names
(`<>:"/\|?*`) become `_`.

> Changing a game's `title` moves its player files to a new folder: players lose their saves and
> settings. Pick the title before releasing the game.



| File | Contents |
|---|---|
| `settings.json` | Values of the `settings.*` variables. Written a moment after the last change. |
| `keybindings.json` | Only the actions whose keys differ from the defaults. Deleting it restores every default. |
| `save.json` | The saved game: game variables, the stack of saved scenes with their parameters and block state, completed and pending events. |
| `log.txt` | Warnings and errors of the last session. |
| `log.previous.txt` | The log of the session before. |

The log is only written when something went wrong, so both developers and players can send it when
reporting a problem. Values in `settings.json` or `save.json` that break the variable rules (edited by
hand, or from an older version of the game) are reset to their defaults and logged; they never stop the
game.

## Display modes

The window cannot be resized by hand. It changes only through the display mode setting (a settings
variable bound to `display.mode`), and that is the only moment the grid is recomputed:

- **windowed**: exactly the minimum grid of `game.json`, scaled to 90 % of the screen.
- **fullscreen** / **borderless**: the minimum grid is scaled to fill the screen on its tighter axis; the
  other axis gets **extra cells**.

So the grid can be bigger than the minimum. Write sizes as percentages, `fill` or `auto`, not as fixed
cells; see [Scenes → Layout](reference/scenes.md#layout).
