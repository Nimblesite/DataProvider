---
layout: layouts/docs.njk
title: Quick Start — Your First Generated Query
description: Write an LQL query, run the DataProvider CLI, and call the generated type-safe extension method in under five minutes.
---

This guide assumes you've already followed [Installation](/docs/installation/) and installed the three dotnet tools (`DataProvider`, `DataProviderMigrate`, `Lql`) plus `Nimblesite.DataProvider.SQLite`.

## 1. Write an LQL query

Create `GetActiveOrders.lql`:

```
Order
|> filter(fn(row) => Order.Status = @status)
|> select(Order.Id, Order.CustomerName, Order.Total)
|> order_by(Order.Id)
```

## 2. Transpile to SQL

```bash
dotnet Lql sqlite --input GetActiveOrders.lql --output GetActiveOrders.generated.sql
```

Add the query to `DataProvider.json`:

```json
{
  "queries": [
    { "name": "GetActiveOrders", "sqlFile": "GetActiveOrders.generated.sql" }
  ],
  "connectionString": "Data Source=app.db"
}
```

## 3. Generate the C# extension method

```bash
dotnet DataProvider sqlite --project-dir . --config DataProvider.json --out ./Generated
```

The generator emits a method with this signature:

```csharp
public static Task<Result<IReadOnlyList<GetActiveOrdersRow>, SqlError>> GetActiveOrdersAsync(
    this SqliteConnection connection,
    string status,
    CancellationToken cancellationToken = default);
```

It also emits `GetActiveOrdersRow` as an immutable record with `Id`, `CustomerName`, `Total` columns.

## 4. Call it

```csharp
using Microsoft.Data.Sqlite;
using Nimblesite.DataProvider.Core;
using MyApp.Generated;

await using var connection = new SqliteConnection("Data Source=app.db");
await connection.OpenAsync();

var result = await connection.GetActiveOrdersAsync(status: "Active");

switch (result)
{
    case Result<IReadOnlyList<GetActiveOrdersRow>, SqlError>.Ok ok:
        foreach (var order in ok.Value)
            Console.WriteLine($"{order.Id}: {order.CustomerName} — {order.Total:C}");
        break;

    case Result<IReadOnlyList<GetActiveOrdersRow>, SqlError>.Error err:
        Console.Error.WriteLine($"Query failed: {err.Value.Message}");
        break;
}
```

This is the **default** code-generation template — it makes failures explicit in the return type so the compiler forces you to handle both outcomes. The template is pluggable; see [Customising generated code](/docs/dataprovider/#customising-generated-code) to emit a different shape.

## Transpiling LQL at runtime

If you prefer to transpile LQL in your app instead of at build time, reference `Nimblesite.Lql.Postgres` (or the SQLite/SQL Server variant) and call the extension method:

```csharp
using Nimblesite.Lql.Core;
using Nimblesite.Lql.Postgres;
using Nimblesite.Sql.Model;

var lql = """
Customer
|> filter(fn(row) => Customer.Active = true)
|> select(Customer.Id, Customer.Name)
""";

var statement = new LqlStatement(lql);
Result<string, SqlError> sqlResult = statement.ToPostgreSql();

var sql = sqlResult switch
{
    Result<string, SqlError>.Ok ok => ok.Value,
    Result<string, SqlError>.Error err => throw new InvalidOperationException(err.Value.Message)
};
```

`.ToSqlite()` and `.ToSqlServer()` are available from `Nimblesite.Lql.SQLite` and `Nimblesite.Lql.SqlServer`.

## Next Steps

- [Getting Started](/docs/getting-started/) — full end-to-end walkthrough
- [DataProvider](/docs/dataprovider/) — source generation reference
- [LQL](/docs/lql/) — the Lambda Query Language
- [Clinical Coding Platform](https://github.com/Nimblesite/ClinicalCoding) — reference implementation
