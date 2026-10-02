# Expressions

A small, statically typed language used wherever content needs a condition or a computed value:

- conditions: `if` (actions, events, choice items, exits, branch cases), `visibleIf`, `enabledIf`,
  `lockedIf`, `when` (timers), NPC and map variant `if`;
- values: `expr` in `set` / `add` actions;
- some block properties: `bar.max`, `bar.min`, `effect.intensity`.

```
game.courage >= 2 && !game.met_shadow
game.gold - price * 2 > 0 ? 'rich' : 'poor'
len(settings.player_name) > 0 && system.language == 'es'
clamp(game.health + rand(1, 6), 0, game.max_health)
```

## Values

| Literal | Type |
|---|---|
| `0`, `42`, `-3` | int (32-bit; overflow is an error) |
| `true`, `false` | bool |
| `'text'` | string. Escape `'` and `\` with `\`: `'it\'s'`. |
| `game.courage` | the variable's declared type |

**Ints are not booleans.** Write `game.count > 0`, not `game.count`. This turns a typo such as
`game.courage && ...` into an error instead of a silent truthy test.

## Operators

From lowest to highest precedence:

| Operators | Operands | Result |
|---|---|---|
| `c ? a : b` | bool, then two values of the same type | that type |
| `\|\|` | bool, bool | bool (short-circuit) |
| `&&` | bool, bool | bool (short-circuit) |
| `==` `!=` | two values of the same type | bool |
| `<` `<=` `>` `>=` | two ints, or two strings (ordinal comparison) | bool |
| `+` `-` | int, int | int |
| `+` | string with string or int | string (concatenation; the int is written as text) |
| `*` `/` `%` | int, int | int. Integer division; `/` and `%` by zero are errors. |
| `!` `-` (unary) | bool / int | bool / int |

Parentheses group as usual.

## Functions

| Function | Signature | Result |
|---|---|---|
| `min(a, b)`, `max(a, b)` | int, int → int | Smaller / bigger. |
| `abs(a)` | int → int | Absolute value. |
| `clamp(v, lo, hi)` | int, int, int → int | `v` limited to `lo..hi` (`lo` if `lo > hi`). |
| `len(s)` | string → int | Number of characters. |
| `lower(s)`, `upper(s)` | string → string | Case conversion. |
| `contains(s, part)` | string, string → bool | Ordinal substring test. |
| `startsWith(s, p)`, `endsWith(s, p)` | string, string → bool | Ordinal prefix / suffix test. |
| `text(key)` | string → string | The translated text of a key in the current language. |
| `rand(lo, hi)` | int, int → int | A random int in `lo..hi`, **inclusive** (`lo` if `hi < lo`). |

`rand` is **only allowed in action values** (`expr`), never in conditions. Conditions are re-evaluated
whenever their variables change, so a random condition would make events and visible elements
flicker. Using it in a condition is a validation error.

## Errors

- **Compile-time**: syntax errors, unknown variables or functions, wrong argument counts, type
  mismatches, a condition that is not a bool, `rand` in a condition. Reported by `--validate` with the
  expression and the place it is used. Expressions in variable templates, events and scene conditions
  are compiled while loading, so they also stop the game from starting.
- **Runtime**: division by zero and int overflow throw an error with the expression text and the values of
  the variables it reads. Content mistakes are meant to be loud.

Expressions are compiled once into closures and cached by their text, so using the same condition in
many places costs nothing extra.
