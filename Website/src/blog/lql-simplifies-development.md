---
layout: layouts/blog.njk
title: "LQL: How Lambda Query Language Simplifies .NET Development"
description: A deep dive into LQL — the functional pipeline-style DSL that transpiles to PostgreSQL, SQLite, or SQL Server from one source file.
date: 2024-04-14
dateModified: 2026-04-12
author: DataProvider Team
tags:
  - .NET
  - LQL
  - post
---

LQL (Lambda Query Language) is a functional pipeline-style DSL that transpiles to SQL. Write database logic once, run it against any supported target.

## Why LQL?

Raw SQL strings are error-prone and differ across database vendors. LQL provides:

- **Write once, deploy everywhere** — the same query transpiles to PostgreSQL, SQLite, and SQL Server
- **Build-time transpilation** — the `Lql` CLI converts `.lql` files to dialect-specific SQL during your build
- **Type-safe consumption** — the `DataProvider` CLI turns the generated SQL into extension methods that return `Result<T, SqlError>`
- **IDE support** — the VS Code extension ships with syntax highlighting and a language server (Rust + ANTLR)

## Basic example

```
Users
|> filter(fn(row) => row.Age > 18 and row.Status = 'active')
|> join(Orders, on = Users.Id = Orders.UserId)
|> group_by(Users.Id, Users.Name)
|> select(Users.Name, sum(Orders.Total) as TotalSpent)
|> order_by(TotalSpent desc)
|> limit(10)
```

Transpile it to SQLite or PostgreSQL:

```bash
dotnet tool install Lql --version {{ versions.lql }}
dotnet Lql sqlite   --input TopSpenders.lql --output TopSpenders.generated.sql
dotnet Lql postgres --input TopSpenders.lql --output TopSpenders.generated.sql
```

## Runtime transpilation

If you prefer to transpile LQL at runtime instead of build time, reference the dialect library:

```bash
dotnet add package Nimblesite.Lql.Postgres --version {{ versions.nimblesite }}
```

```csharp
using Nimblesite.Lql.Core;
using Nimblesite.Lql.Postgres;
using Nimblesite.Sql.Model;

var statement = new LqlStatement(lqlSource);
Result<string, SqlError> result = statement.ToPostgreSql();
```

`ToSqlite()` and `ToSqlServer()` are also available.

## Pipeline operators

The transpiler supports the full set of pipeline operators — `filter`, `select`, `join` / `left_join` / `right_join` / `full_join`, `group_by`, `having`, `order_by`, `limit`, `offset`, `distinct` — plus aggregates (`sum`, `avg`, `count`, `min`, `max`) and arithmetic expressions.

## Getting started

- [LQL documentation](/docs/lql/)
- [Installation guide](/docs/installation/)
- [Clinical Coding Platform](https://github.com/Nimblesite/ClinicalCoding) — LQL in production-style use
