---
layout: layouts/blog.njk
title: "DataProvider 0.11.0: The First Stable Release"
description: DataProvider 0.11.0 is the first release without a pre-release tag. It ships native DataProviderMigrate binaries on Homebrew and Scoop, and this post lists everything the toolkit does today.
date: 2026-10-14
author: DataProvider Team
tags:
  - .NET
  - Database
  - post
---

DataProvider 0.11.0 is out. After 25 beta releases, it is the first version published without a pre-release suffix, and the first to go through the whole release pipeline: NuGet, GitHub Releases, Homebrew and Scoop.

## Why this release matters

Until now, every package was a pre-release. `dotnet add package` and `dotnet tool install` skip pre-releases, so you needed an explicit version or `--prerelease` to install anything. 0.11.0 is the version they now install by default:

```bash
dotnet tool install DataProviderMigrate
dotnet add package Nimblesite.DataProvider.SQLite
```

A release is also a stronger promise than it used to be. A version tag publishes nothing until the full CI suite and CodeQL analysis pass on the tagged commit. NuGet packages are published through trusted publishing, so the release no longer uses a stored NuGet API key. The native binaries come with signed checksums and a CycloneDX SBOM per archive.

## What's new in 0.11.0

### Native DataProviderMigrate binaries

The migration CLI now ships as a self-contained Native AOT executable for Linux x64, Windows x64, and macOS on Apple Silicon and Intel. It needs no .NET runtime.

```bash
# macOS (Apple Silicon)
brew install nimblesite/tap/dataprovider-migrate

# Windows
scoop bucket add nimblesite https://github.com/Nimblesite/scoop-bucket
scoop install dataprovider-migrate
```

Linux and Intel Mac archives are attached to the [GitHub release](https://github.com/Nimblesite/DataProvider/releases). The native binary runs `migrate` against SQLite and PostgreSQL. `export`, which loads your compiled assembly, and the SQL Server provider stay in the `DataProviderMigrate` dotnet tool. Every pull request now builds the native Linux binary and runs it against a real SQLite database, so the native build cannot quietly break.

### The LQL language server on Homebrew and Scoop

`lql-lsp`, the Rust language server behind the VS Code extension, installs the same way:

```bash
brew install nimblesite/tap/lql-lsp
scoop install lql-lsp
```

### Smaller changes

- `DataProviderMigrate --version` prints the installed version.
- Duplicated code across DataProvider, LQL, Migration and Sync is down to under 15%, and CI now enforces that budget.

## Everything DataProvider does today

Each component works on its own. Use what you need.

### DataProvider: typed C# from your SQL

- The `DataProvider` dotnet tool turns `.sql` and `.lql` files into C# extension methods, with `sqlite`, `postgres` and `sqlserver` commands.
- Runtime packages for SQLite, PostgreSQL and SQL Server.
- Typed row and parameter records, and grouping configuration that builds parent and child models from joined rows.
- Generated methods return `Result<T, SqlError>` by default. Four code-template delegates in `CodeGenerationConfig` let you change the method signature, the model types, grouped models and the file layout.
- The generated code is plain ADO.NET with no reflection.

### LQL: one query language, three databases

- A pipeline query language: `filter`, `select`, inner, left, right and full joins, `group_by`, `order_by`, `limit`, `distinct`, `let` bindings and subqueries.
- Transpiles to PostgreSQL, SQLite and SQL Server, at build time with the `Lql` tool or at runtime with `ToPostgreSql()`, `ToSqlite()` and `ToSqlServer()`.
- A VS Code extension and a Rust language server with schema-aware completions from your live database, hover documentation, real-time diagnostics, document symbols and optional AI completions through Ollama.
- An F# type provider that validates LQL at compile time.
- Documentation and an in-browser playground at [lql.dev](https://lql.dev).

### Migrations: YAML schemas

- Define schemas in YAML and apply them with `DataProviderMigrate migrate` to SQLite, PostgreSQL or SQL Server. `export` turns an existing C# schema class into YAML.
- Migrations are computed as a diff against the live database. Re-running against a converged database applies nothing. Destructive changes need `--allow-destructive`, and `--phase` applies the structural and row-level-security changes separately.
- On SQLite and PostgreSQL, every run checks the resulting schema against your YAML.
- Row-level security from YAML: native policies on PostgreSQL, and trigger and secure-view emulation on SQLite. Predicates are written in portable LQL, with raw SQL available on PostgreSQL.
- Declarative trigger guards: row-level `BEFORE` triggers declared in YAML with an LQL `raiseWhen` predicate, on SQLite and PostgreSQL.
- Library packages for SQLite, PostgreSQL and SQL Server if you want to drive migrations from code.

### Sync: offline-first replication

- Bidirectional sync with per-row version vectors, watermarks and tombstones, so only changed rows move and deleted rows stay deleted.
- Configurable conflict resolution, last-writer-wins by default.
- Mappings that sync between replicas whose tables and columns differ.
- An HTTP transport with Server-Sent Events subscriptions for live updates.
- Replica providers for SQLite, PostgreSQL and SQL Server.

### SQL model

- `Nimblesite.Sql.Model`, the SQL object model and query builder the rest of the toolkit is built on.

### Reporting (preview)

- A reporting engine, API and React renderer live in the repository. They are not part of the 0.11.0 package set yet.

### Distribution

- 18 library packages and 3 dotnet tools on NuGet: `DataProvider`, `DataProviderMigrate` and `Lql`.
- Native `DataProviderMigrate` and `lql-lsp` binaries for Linux x64, Windows x64, and macOS on Apple Silicon and Intel.
- Homebrew (Apple Silicon) and Scoop (Windows) packages for both.

## Get started

Install the tools and a runtime package with the [installation guide](/docs/installation/), or follow [Getting Started](/docs/getting-started/) for an end-to-end walkthrough. The [Nimblesite Clinical Coding Platform](https://github.com/Nimblesite/ClinicalCoding) shows every component working together in one application.
