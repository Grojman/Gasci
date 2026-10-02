# Versioning and releases

Gasci follows [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html): `MAJOR.MINOR.PATCH`.

The current version is declared **once**, in [`Directory.Build.props`](../Directory.Build.props)
(`<Version>`), and applies to every project of the solution (it ends up in the assembly and file
versions of the executable).

## What the version number means

Gasci is a data-driven engine, so its public "API" is mostly the **content format**: the files in
`assets/` and the meaning of every property in them. The rules below are about that format first, and
about the C# API second.

| Bump | When | Examples |
|---|---|---|
| **MAJOR** (`2.0.0`) | Existing, valid content stops loading or behaves differently. Existing player files (saves, settings, key bindings) stop loading. | Renaming or removing a block type, property, action, trigger or binding; changing a default value; changing the meaning of an expression operator; a new mandatory field; a new save format that cannot read the old one. |
| **MINOR** (`1.1.0`) | New things that existing content does not need to know about. | A new block, control, action, trigger, effect, function, inline code or optional property; a new `game.json` setting with a default that keeps the old behaviour; new validator **warnings**. |
| **PATCH** (`1.0.1`) | Fixes that make the engine do what the documentation already says. | Bug fixes, performance, clearer error messages, documentation. |

Notes:

- A new validator **error** for content that used to pass is a MAJOR change, unless that content was
  already broken at runtime (then it is a fix).
- Changes to the demo game (Relato) in `assets/` alone do not change the engine version. They are listed
  in the changelog under a *Demo* heading when they matter.
- While the version is below the next major, deprecated features keep working and `--validate` warns
  about them. They are removed in the next MAJOR.

## Tags and branches

- `main` is always releasable: it builds, the tests pass and `--validate` passes.
- Every release is an annotated git tag `vMAJOR.MINOR.PATCH` (for example `v1.0.0`) on `main`.
- Each tag has a GitHub release whose notes are the changelog entry of that version.

## Making a release

1. Make sure `main` is clean and up to date:

   ```bash
   git switch main && git pull
   dotnet build && dotnet test && dotnet run --project src/Relato -- --validate
   ```

2. Pick the new version with the table above.
3. In [`CHANGELOG.md`](../CHANGELOG.md), rename `[Unreleased]` to `[x.y.z] - YYYY-MM-DD`, add a new empty
   `[Unreleased]` above it, and update the comparison links at the bottom.
4. Set `<Version>x.y.z</Version>` in `Directory.Build.props`.
5. Commit and tag:

   ```bash
   git commit -am "Release x.y.z"
   git tag -a vx.y.z -m "Gasci x.y.z"
   git push origin main vx.y.z
   ```

6. On GitHub, *Releases → Draft a new release*, choose the tag, title it `Gasci x.y.z`, and paste the
   changelog entry as the notes. Optionally attach `dotnet publish` builds (see
   [Getting started](getting-started.md#publishing-a-build)).

## History

| Version | Date | Summary |
|---|---|---|
| [1.0.0](https://github.com/Grojman/Gasci/releases/tag/v1.0.0) | 2026-10-02 | First public version. |
