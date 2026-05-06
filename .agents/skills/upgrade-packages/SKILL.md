---
name: upgrade-packages
description: Upgrades all dependencies to latest versions across C#, Rust, and TypeScript. Use when the user says "upgrade packages", "update dependencies", "bump versions", or "upgrade deps".
argument-hint: "[language: dotnet|rust|typescript|all]"
---
<!-- agent-pmo:d75d5c8 -->

# Upgrade Packages

Upgrade all dependencies to their latest versions.

## Steps

### Step 1 — Detect packages to upgrade

Based on `$ARGUMENTS` (default: all):

**C# (.NET):**
- Check `Directory.Build.props` for centrally managed package versions
- Check individual `.csproj` files for project-specific packages
- Run `dotnet list package --outdated` on `DataProvider.sln`

**Rust:**
- Check `Lql/lql-lsp-rust/Cargo.toml` workspace dependencies
- Run `cd Lql/lql-lsp-rust && cargo outdated` (install with `cargo install cargo-outdated` if needed)

**TypeScript:**
- Check `Lql/LqlExtension/package.json`
- Run `cd Lql/LqlExtension && npm outdated`

### Step 2 — Upgrade

**C# (.NET):**
- Update version numbers in `Directory.Build.props` for central packages
- For project-specific packages: `dotnet add <project> package <name>`
- Run `dotnet restore`

**Rust:**
- Update versions in `Cargo.toml`
- Run `cargo update`

**TypeScript:**
- Run `npm update` or manually update `package.json` for major versions
- Run `npm install`

### Step 3 — Verify

1. Run `make ci` — must pass completely
2. If any tests fail, investigate whether the failure is from the upgrade
3. Report which packages were upgraded and from/to versions

## Rules

- Never downgrade a package
- If a major version upgrade breaks tests, report it and revert that specific upgrade
- Always run the full test suite after upgrading
- Update lock files (`Cargo.lock`, `package-lock.json`) as part of the upgrade
