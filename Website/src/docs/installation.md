---
layout: layouts/docs.njk
title: Installation Guide
description: Install the DataProvider, DataProviderMigrate, and Lql dotnet tools plus the Nimblesite runtime NuGet packages in your .NET 10 project.
---

DataProvider ships in two halves:

1. **Three dotnet CLI tools** that run at build time to generate code, migrate schemas, and transpile queries.
2. **Runtime library packages** (`Nimblesite.*`) that your app references to execute the generated code.

You need both.

## 1. Install the CLI tools

Create a local tool manifest in your repository root, then pin each tool:

```bash
dotnet new tool-manifest
dotnet tool install DataProvider --version {{ versions.dataprovider }}
dotnet tool install DataProviderMigrate --version {{ versions.dataproviderMigrate }}
dotnet tool install Lql --version {{ versions.lql }}
```

This writes `.config/dotnet-tools.json`. Commit it. Every developer and CI runner restores the same versions with `dotnet tool restore`.

| Tool | Command | Version | What it does |
|------|---------|---------|--------------|
| `DataProvider` | `dotnet DataProvider` | `{{ versions.dataprovider }}` | Source generation — reads `DataProvider.json` + `.sql`/`.lql` files, emits C# extension methods |
| `DataProviderMigrate` | `dotnet DataProviderMigrate` | `{{ versions.dataproviderMigrate }}` | YAML-schema migration CLI (`migrate`, `export`) |
| `Lql` | `dotnet Lql` | `{{ versions.lql }}` | LQL → SQL transpiler (`sqlite`, `postgres`) |

## 2. Add the runtime library packages

Pick the packages for your database and the features you use. All runtime packages are `{{ versions.nimblesite }}`.

### DataProvider runtime

```bash
# Pick ONE database runtime
dotnet add package Nimblesite.DataProvider.SQLite
dotnet add package Nimblesite.DataProvider.Postgres
dotnet add package Nimblesite.DataProvider.SqlServer
```

`Nimblesite.DataProvider.Core` is pulled in transitively — you rarely reference it directly.

### LQL (Lambda Query Language)

Only needed if you embed LQL transpilation in your application (the `Lql` CLI tool handles build-time transpilation without these packages).

```bash
dotnet add package Nimblesite.Lql.SQLite
dotnet add package Nimblesite.Lql.Postgres
dotnet add package Nimblesite.Lql.SqlServer
```

### Sync framework (optional)

```bash
dotnet add package Nimblesite.Sync.Core
dotnet add package Nimblesite.Sync.Http
dotnet add package Nimblesite.Sync.Postgres
dotnet add package Nimblesite.Sync.SQLite
```

### Reporting (optional)

```bash
dotnet add package Nimblesite.Reporting.Engine
```

### Migration libraries (optional)

Use the `DataProviderMigrate` CLI for most cases. Reference these only if you embed schema migration in your own app code.

```bash
dotnet add package Nimblesite.DataProvider.Migration.Core
dotnet add package Nimblesite.DataProvider.Migration.SQLite
```

## 3. Requirements

- **.NET 10 SDK** or later
- C# latest language version
- `<Nullable>enable</Nullable>`

Add the following to your `.csproj`:

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <Nullable>enable</Nullable>
  <LangVersion>latest</LangVersion>
</PropertyGroup>
```

## Next Steps

- [Getting Started](/docs/getting-started/) — end-to-end walkthrough using all three CLI tools
- [Quick Start](/docs/quick-start/) — 5-minute generated-query example
- [Clinical Coding Platform](https://github.com/Nimblesite/ClinicalCoding) — the full reference implementation using every package above
