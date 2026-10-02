# Changelog

All notable changes to the Gasci engine are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the engine follows
[Semantic Versioning](https://semver.org/spec/v2.0.0.html). What counts as a breaking change for a
data-driven engine is defined in [`docs/versioning.md`](docs/versioning.md).

## [Unreleased]

## [1.0.0] - 2026-10-02

First public version of the engine, extracted from the game *Relato* (which ships with it as the demo).

### Added

- **Scenes built from blocks**, loaded from `assets/data/scenes/*.json`, on a scene stack where only the
  top scene is updated and receives input. Presentations `fill`, `modal` and custom, with `none`, `dim`
  and `opaque` backdrops. Scene parameters (`$name`), `onEnter` / `onResume` / `onLeave` hooks and saved
  scenes.
- **Layout engine**: `auto`, `fill`, percentage and fixed sizes, min/max limits, anchors and offsets,
  percentage distribution that carries the rounding remainder, block culling and dirty redraws.
- **Blocks**: `stack`, `overlay`, `menu`, `branch`, `text`, `textbox`, `image`, `map`, `conversation`,
  `choices`, `card`, `bar`, `effect` (`dissolve`, `glitch`), `timer`, `spacer`.
- **Menu controls**: `button`, `switch`, `slider`, `combobox`, `numberfield`, `textfield`, `keybind`.
- **Typed variables** (`int`, `bool`, `string`) in four scopes (`game.`, `settings.`, `session.`,
  `system.`), with rules (`min`, `max`, `maxLength`, `options`, `optionsFrom`), engine bindings and
  debounced persistence.
- **Expression language** with static type checking, integer arithmetic, string operations and built-in
  functions, compiled and cached.
- **One action model** shared by events, buttons, controls, scene and block inputs, hooks and timers.
- **Event engine** with `immediate`, `enterMap`, `step`, `halfway`, `input` and `change` triggers,
  indexed by trigger and by the variables each condition reads.
- **Input actions** declared in data, rebindable by the player (with swap on conflict), per-context
  conflict detection and a controls page.
- **Tile maps** split into sections, with solid / interactive / animated tiles, exits, NPCs with
  conditional variants and conditional map drawings.
- **Branching conversations** with portraits, options, locked options, timed locks and variable writes.
- **ASCII images** with CSS-like `fit` / `position` and animated drawing (`drawMode`, `cursorMode`).
- **Localisation** through `dialogs.csv`, with inline codes for colour, speed, pauses, variables and key
  names, plus pre-computed text metrics.
- **Display modes** (windowed, fullscreen, borderless) with a grid that grows to fill the screen.
- **Custom bitmap fonts** with a character map.
- **Audio** with synthesized placeholders replaceable by `.wav` files.
- **Save / continue and autosave**, settings and key binding files, and a per-session log.
- **`--validate`**: a headless check of every asset and cross-reference that also regenerates the text
  metrics.
- **Unit tests** (xUnit) for expressions, layout, variables, input and events.
- **Documentation**: README, getting started guide, tutorial, architecture overview and a full content
  reference in `docs/`.

[Unreleased]: https://github.com/Grojman/Gasci/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/Grojman/Gasci/releases/tag/v1.0.0
