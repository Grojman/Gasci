# Gasci

**Gasci** is a data-driven engine for keyboard-only, terminal-style ASCII games, written in C# on top of
[SadConsole](https://github.com/Thraka/SadConsole) (MonoGame host).

Everything a game shows and does is **data**: screens, menus, variables, keys, events, maps,
conversations, texts and ASCII images live in JSON, CSV and text files under `assets/`. The C# code is
only the engine. A new game is made by writing new assets, not new code.

The repository also contains **Relato**, a short horror graphic novel with exploration. It is the game
Gasci was born from and serves as the reference demo for every feature.

> **Version 1.0.0** — see the [changelog](CHANGELOG.md) and the [versioning policy](docs/versioning.md).

---

## Features

| Area | What the engine gives you |
|---|---|
| **Scenes** | Every screen is a JSON tree of blocks on a scene stack. Full-screen and modal presentations, dimmed backdrops, parameters, enter/resume/leave hooks. |
| **Layout** | `auto`, `fill`, percentages and cells, min/max, anchors and offsets. Layouts adapt to any screen size and to every language. |
| **Blocks** | Containers, texts, typewriter text boxes, ASCII images, tile maps, conversations, choice lists, black cards, gauges, effects, timers. |
| **Menus** | Buttons, switches, sliders, combo boxes, number fields, text fields and a key rebinding control. |
| **Variables** | Typed (`int`, `bool`, `string`), scoped (`game.`, `settings.`, `session.`, `system.`), validated, saved and debounced to disk. |
| **Expressions** | A small typed language for conditions and computed values, checked when the game loads. |
| **Logic** | One action model for buttons, events, timers and hooks. Events fire on map entry, steps, crossing a section, input, value changes or as soon as a condition holds. |
| **Maps** | ASCII tile maps split into sections, with solid, interactive and animated tiles, exits, NPCs and drawings that change with the story. |
| **Conversations** | Branching dialogs with animated portraits, conditional and locked options, and options that lock if the player hesitates. |
| **Input** | Every key is an action declared in data. Players rebind keys in game; conflicts are swapped, never saved. |
| **Localisation** | All texts are keys in one CSV, with inline codes for colour, typing speed, pauses, variables and bound key names. |
| **Display** | Windowed, fullscreen and borderless modes; custom bitmap fonts with a character map. |
| **Persistence** | Save / continue, optional autosave, settings and key binding files, a per-session log. |
| **Tooling** | `--validate` checks every asset and every cross-reference without opening a window. |

The engine is **keyboard only** by design: the mouse is disabled.

## Quick start

Requires the [.NET 9 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/Grojman/Gasci.git
cd Gasci
dotnet run --project src/Relato                 # play the demo
dotnet run --project src/Relato -- --validate   # check every asset without opening a window
dotnet test                                     # run the unit tests
```

Default controls (all rebindable in *Settings → Controls*): arrows / WASD to move, E / Space to
interact, Enter / Space / E to accept, Esc for the pause menu and to go back.

More in [Getting started](docs/getting-started.md).

## A taste of the data

A button that buys a potion, only enabled when the player can afford it:

```jsonc
{ "type": "button", "label": "shop.buy_potion", "enabledIf": "game.gold >= 10",
  "actions": [
    { "type": "add", "var": "game.gold", "value": -10 },
    { "type": "add", "var": "game.potions", "value": 1 },
    { "type": "sfx", "id": "coin" }
  ] }
```

An event that fires once, when the player crosses the middle of the second section of the street:

```jsonc
{
  "id": "shadow_appears",
  "if": "!game.met_shadow",
  "trigger": { "type": "halfway", "map": "street", "section": 1 },
  "actions": [
    { "type": "sfx", "id": "creature" },
    { "type": "message", "target": "map_message", "keys": ["ev.shadow_appears"] },
    { "type": "dialog", "id": "shadow_encounter" }
  ]
}
```

## Documentation

| Document | Contents |
|---|---|
| [Getting started](docs/getting-started.md) | Requirements, running, validating, testing, publishing, player files. |
| [Making a game](docs/making-a-game.md) | A step-by-step tutorial: a new map, an NPC, a conversation, an event and a menu. |
| [Architecture](docs/architecture.md) | How the engine is organised: start-up, the frame, the scene stack, focus, events, rendering. |
| [Validation](docs/validation.md) | What `--validate` checks, errors vs. warnings, and the generated text metrics. |
| **Content reference** | |
| [`game.json`](docs/reference/game-config.md) | Grid, font, map sections, images, start scene, new game, persistence. Display modes. |
| [Variables](docs/reference/variables.md) | Scopes, templates, rules, bindings, autosave. |
| [Expressions](docs/reference/expressions.md) | Grammar, types, operators, functions, errors. |
| [Input](docs/reference/input.md) | Input actions, contexts, defaults, rebinding. |
| [Scenes](docs/reference/scenes.md) | Scene files, presentation, layout, focus, rendering. |
| [Blocks](docs/reference/blocks.md) | Every block and control, with all its properties and defaults. |
| [Actions](docs/reference/actions.md) | Every action type and its fields. |
| [Events](docs/reference/events.md) | Triggers, conditions, `once`, when events run. |
| [Maps](docs/reference/maps.md) | Map drawings, tiles, exits, NPCs, variants, sections. |
| [Conversations](docs/reference/conversations.md) | Nodes, options, branches, portraits. |
| [Texts](docs/reference/texts.md) | `dialogs.csv`, languages, inline codes, text metrics. |
| [Images, theme, fonts and audio](docs/reference/media.md) | ASCII images, draw animations, colours, bitmap fonts, sounds. |
| **Project** | |
| [Versioning and releases](docs/versioning.md) | Semantic versioning for a data engine, and how a release is made. |
| [Roadmap](docs/roadmap.md) | Planned features and known limitations. |
| [Design notes](docs/design/ui-system-design.md) | The design record of the engine and the reasoning behind its decisions. |
| [Relato GDD](gdd.md) | The design document of the demo game (Spanish). |

## Repository layout

```
Gasci/
├── assets/                  The game: everything here is data (Relato, the demo)
│   ├── data/                game.json, variables, input, scenes, maps, events, conversations, texts, theme
│   ├── images/              ASCII art (.txt)
│   ├── audio/               Optional .wav files that replace the synthesized sounds
│   └── fonts/               Optional custom bitmap fonts
├── src/Relato/              The engine (C#) and the executable
├── tests/Relato.Tests/      Unit tests (xUnit)
├── docs/                    Documentation
├── Directory.Build.props    Engine version, shared by every project
├── CHANGELOG.md
└── gdd.md                   Design document of the demo game
```

> The C# project, namespace and user data folder are still called `Relato`, the game the engine was
> extracted from. Renaming them to `Gasci` is planned (see the [roadmap](docs/roadmap.md)); it does not
> affect content files.

## The demo: Relato

A short playable slice. You wake up at home. Your mother asks whether you slept, and lying adds
`game.distortion`. On the street, crossing the middle of the second section summons *La Sombra*: facing
it adds `game.courage`, while running or agreeing with it adds distortion. With courage, the locked
options with your mother and your neighbour Clara open up. With any distortion the `glitch` effect
flickers over the map, and at distortion ≥ 2 the street is redrawn distorted. At distortion ≥ 3 Clara
"dies" and her icon changes for the rest of the game. The bench in the park triggers one of two endings
depending on courage.

Every one of those behaviours is data in `assets/`, which makes the demo the best example of how to use
the engine.

## Contributing

1. Run `dotnet test` and `dotnet run --project src/Relato -- --validate` before every commit; both must pass.
2. Content changes go with their documentation: a new block property, action or trigger is not done until
   it is in `docs/reference/`.
3. Add an entry under `[Unreleased]` in [CHANGELOG.md](CHANGELOG.md) for every user-visible change.
4. Releases follow [docs/versioning.md](docs/versioning.md).
