---
name: upgrade-packages
description: Upgrade all dependencies/packages to their latest versions for the detected language(s). Use when the user says "upgrade packages", "update dependencies", "bump versions", "update packages", or "upgrade deps".
argument-hint: "[--check-only] [--major] [package-name]"
---
<!-- agent-pmo:74cf183 -->

# Upgrade Packages

Upgrade all project dependencies to their latest compatible (or latest major, if `--major`) versions.
This repo has C#/.NET (NuGet), Rust (cargo), and TypeScript/Node (npm) — process all three when running without a package-name argument.

## Arguments

- `--check-only` — List outdated packages without upgrading. Stop after Step 2.
- `--major` — Include major version bumps (breaking changes). Without this flag, stay within semver-compatible ranges.
- Any other argument is treated as a specific package name to upgrade (instead of all packages).

## Step 1 — Detect language and package manager

Inspect manifest files. For this repo, the relevant manifests are:

| Manifest file | Language | Package manager |
|---|---|---|
| `Cargo.toml` (workspace at `Lql/lql-lsp-rust/Cargo.toml`) | Rust | cargo |
| `Lql/LqlExtension/package.json` | TypeScript | npm |
| `Website/package.json` | TypeScript / static site | npm |
| `Directory.Build.props` + `**/*.csproj`, `**/*.fsproj`, `DataProvider.sln` | C# / F# | NuGet (dotnet) |

Process each in order: NuGet → cargo → npm.

## Step 2 — List outdated packages

Run the listing command for each manager BEFORE upgrading. Show the user what will change.

### C# / F# (NuGet)
```bash
dotnet list package --outdated
dotnet list package --outdated --include-transitive
```
**Read the docs:** https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-list-package

### Rust
```bash
cd Lql/lql-lsp-rust && cargo outdated        # install: cargo install cargo-outdated
cd Lql/lql-lsp-rust && cargo update --dry-run
```
**Read the docs:** https://doc.rust-lang.org/cargo/commands/cargo-update.html

### Node.js (npm) — LqlExtension
```bash
cd Lql/LqlExtension && npm outdated
```
### Node.js (npm) — Website
```bash
cd Website && npm outdated
```
**Read the docs:** https://docs.npmjs.com/cli/v10/commands/npm-update

If `--check-only` was passed, **stop here** and report the outdated lists.

## Step 3 — Read the official upgrade docs

**Before running any upgrade command, you MUST fetch and read the official documentation URL listed above for the detected package manager.** Use WebFetch to retrieve the page. This ensures you use the correct flags and understand the behavior. Do not guess at flags or options from memory.

## Step 4 — Upgrade packages

Run the upgrade. If a specific package name was given as an argument, upgrade only that package (in the manager that owns it).

### C# / F# (NuGet)
There is NO single `dotnet upgrade-all` command. Choose one:

Option A — manual per package (most controlled):
```bash
dotnet add <project.csproj> package <PackageName>                     # upgrades to latest
dotnet add <project.csproj> package <PackageName> --version <version> # specific version
```
For `Directory.Build.props`, edit the version numbers directly in the XML — that's where shared package versions live in this repo.

Option B — global tool:
```bash
dotnet tool install --global dotnet-outdated-tool
dotnet outdated --upgrade
```
**Read the docs:** https://github.com/dotnet-outdated/dotnet-outdated and https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-add-package

### Rust
```bash
cd Lql/lql-lsp-rust && cargo update                          # semver-compatible updates
# --major flag:
cd Lql/lql-lsp-rust && cargo update --breaking               # major version bumps (cargo 1.84+)
```
The lints workspace (`[workspace.lints]`) and pinned ANTLR-runtime version may require special handling — check `Cargo.toml` before bumping the antlr crate; the parser is regenerated against a specific ANTLR runtime per CLAUDE.md.

### Node.js (npm)
```bash
cd Lql/LqlExtension && npm update                            # semver-compatible
cd Website && npm update
# --major flag:
cd Lql/LqlExtension && npx npm-check-updates -u && npm install
cd Website && npx npm-check-updates -u && npm install
```
**Read the docs:** https://docs.npmjs.com/cli/v10/commands/npm-update

## Step 5 — Verify the upgrade

After upgrading, run the project's build and test suite to confirm nothing broke:

```bash
make ci
```

If `make ci` is unavailable for some reason, fall back to running each affected target manually:

```bash
make _build_dotnet && make _test_dotnet
make _build_rust && make _test_rust
make _build_ts && make _test_ts
```

If tests fail:
1. Read the failure output carefully.
2. Check the changelog / migration guide for the upgraded packages (fetch the release notes URL if available).
3. Fix breaking changes in the code.
4. Re-run tests.
5. If stuck after 3 attempts on the same failure, report it to the user with the error details and the package that caused it.

## Step 6 — Report

Provide a summary:

- Packages upgraded (old version → new version), grouped by NuGet / cargo / npm.
- Packages skipped (and why, e.g., major version bump without `--major`).
- Build/test result after upgrade.
- Any breaking changes that were fixed.
- Any packages that could not be upgraded (with error details).

## Rules

- **Always list outdated packages first** before upgrading anything.
- **Always read the official docs** for the package manager before running upgrade commands.
- **Always run `make ci` after upgrading** to catch breakage immediately.
- **Never remove packages** unless they were explicitly deprecated and replaced.
- **Never downgrade packages** unless rolling back a broken upgrade.
- **Never modify lockfiles manually** (`package-lock.json`, `Cargo.lock`, `packages.lock.json`) — let the package manager regenerate them.
- **Never bump the ANTLR runtime crate without regenerating** the parser from `*.g4` per CLAUDE.md ("Always upgrade to the latest version of ANTLR to make sure the parser is correct"). Mismatched runtime ⇄ generated code = silent breakage.
- **Commit nothing** — leave changes in the working tree for the user to review.

## Success criteria

- All outdated packages upgraded to latest compatible (or latest major if `--major`)
- `make ci` passes
- User has a clear summary of what changed
