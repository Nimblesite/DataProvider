---
layout: layouts/blog.njk
title: Getting Started with the DataProvider Toolkit
description: An introduction to using the DataProvider toolkit in .NET — install the CLI tools, add a runtime package, and generate your first type-safe query.
date: 2024-04-20
dateModified: 2026-04-12
author: DataProvider Team
tags:
  - .NET
  - C#
  - post
---

DataProvider is a complete toolkit for .NET data access. In this post we walk through the basics: installing the CLI tools, adding the runtime package, and generating your first type-safe query.

## Installation

DataProvider ships as three **dotnet CLI tools** plus runtime libraries. Add a local tool manifest and install the tools you need:

```bash
dotnet new tool-manifest
dotnet tool install DataProvider --version {{ versions.dataprovider }}
dotnet tool install DataProviderMigrate --version {{ versions.dataproviderMigrate }}
dotnet tool install Lql --version {{ versions.lql }}
```

Then add the runtime package for your database:

```bash
dotnet add package Nimblesite.DataProvider.SQLite --version {{ versions.nimblesite }}
```

## Your First Query

Write a query in LQL, transpile it to SQL, and generate a type-safe C# extension method:

```csharp
using Microsoft.Data.Sqlite;
using Nimblesite.DataProvider.Core;
using MyApp.Generated;

await using var connection = new SqliteConnection("Data Source=app.db");
await connection.OpenAsync();

var result = await connection.GetActiveOrdersAsync(status: "Active");

if (result is Result<IReadOnlyList<GetActiveOrdersRow>, SqlError>.Ok ok)
{
    foreach (var order in ok.Value)
        Console.WriteLine($"{order.Id}: {order.CustomerName}");
}
```

Every generated method returns `Result<T, SqlError>` — no exceptions, no reflection, pure ADO.NET under the hood.

## Type Safety

The `DataProvider` CLI validates every query against your schema at build time and emits C# extension methods with fully-typed row records. Invalid queries become **compilation errors**.

## Next Steps

- [Installation](/docs/installation/) — the full package reference
- [Getting Started](/docs/getting-started/) — end-to-end walkthrough
- [Clinical Coding Platform](https://github.com/Nimblesite/ClinicalCoding) — reference implementation
