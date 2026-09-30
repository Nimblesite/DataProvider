---
layout: layouts/docs.njk
title: Getting Started with DataProvider
description: End-to-end walkthrough — install the three dotnet tools, define a YAML schema, write an LQL query, generate C# extension methods, and run your first query.
---

This walkthrough takes you from an empty folder to a running, type-safe database query using the full DataProvider toolchain: `DataProviderMigrate` for schema, `Lql` for queries, and `DataProvider` for C# code generation.

## Prerequisites

- **.NET 10 SDK** or later
- A terminal

No database server required — we use SQLite.

## 1. Create the project

```bash
mkdir MyApp && cd MyApp
dotnet new console
dotnet new tool-manifest
```

## 2. Install the CLI tools

```bash
dotnet tool install DataProvider --version {{ versions.dataprovider }}
dotnet tool install DataProviderMigrate --version {{ versions.dataproviderMigrate }}
dotnet tool install Lql --version {{ versions.lql }}
```

## 3. Add the runtime package

```bash
dotnet add package Nimblesite.DataProvider.SQLite --version {{ versions.nimblesite }}
```

Set the target framework in `MyApp.csproj`:

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <Nullable>enable</Nullable>
  <LangVersion>latest</LangVersion>
</PropertyGroup>
```

## 4. Define the schema

Create `example-schema.yaml`:

```yaml
name: example
tables:
  - name: Customer
    schema: main
    columns:
      - name: Id
        type: Text
        isNullable: false
      - name: CustomerName
        type: Text
        isNullable: false
      - name: Email
        type: Text
        isNullable: true
    primaryKey:
      columns:
        - Id
```

Generate the database:

```bash
dotnet DataProviderMigrate migrate \
  --schema example-schema.yaml \
  --output app.db \
  --provider sqlite
```

## 5. Write a query in LQL

Create `GetCustomers.lql`:

```
Customer
|> filter(fn(row) => (@customerId IS NULL OR Customer.Id = @customerId))
|> select(Customer.Id, Customer.CustomerName, Customer.Email)
|> order_by(Customer.CustomerName)
```

Transpile it to SQLite:

```bash
dotnet Lql sqlite \
  --input GetCustomers.lql \
  --output GetCustomers.generated.sql
```

## 6. Configure DataProvider

Create `DataProvider.json`:

```json
{
  "queries": [
    {
      "name": "GetCustomers",
      "sqlFile": "GetCustomers.generated.sql"
    }
  ],
  "tables": [
    {
      "schema": "main",
      "name": "Customer",
      "generateInsert": true,
      "generateUpdate": true,
      "generateDelete": true,
      "primaryKeyColumns": ["Id"]
    }
  ],
  "connectionString": "Data Source=app.db"
}
```

Generate the C# extension methods:

```bash
dotnet DataProvider sqlite \
  --project-dir . \
  --config DataProvider.json \
  --out ./Generated
```

## 7. Wire everything into MSBuild

Instead of running the tools manually, add targets to `MyApp.csproj` so every build regenerates everything:

```xml
<Target Name="RunDataProviderMigrate" BeforeTargets="CoreCompile">
  <Exec Command="dotnet DataProviderMigrate migrate --schema example-schema.yaml --output app.db --provider sqlite" />
</Target>

<Target Name="RunLqlTranspiler" BeforeTargets="CoreCompile" DependsOnTargets="RunDataProviderMigrate">
  <Exec Command="dotnet Lql sqlite --input GetCustomers.lql --output GetCustomers.generated.sql" />
</Target>

<Target Name="RunDataProvider" BeforeTargets="CoreCompile" DependsOnTargets="RunLqlTranspiler">
  <Exec Command="dotnet DataProvider sqlite --project-dir . --config DataProvider.json --out ./Generated" />
  <ItemGroup>
    <Compile Include="Generated/**/*.g.cs" />
  </ItemGroup>
</Target>
```

## 8. Consume the generated extension method

Edit `Program.cs`:

```csharp
using Microsoft.Data.Sqlite;
using Nimblesite.DataProvider.Core;
using MyApp.Generated;

await using var connection = new SqliteConnection("Data Source=app.db");
await connection.OpenAsync();

var result = await connection.GetCustomersAsync(customerId: null);

var message = result switch
{
    Result<IReadOnlyList<GetCustomersRow>, SqlError>.Ok ok =>
        $"Found {ok.Value.Count} customers",
    Result<IReadOnlyList<GetCustomersRow>, SqlError>.Error err =>
        $"Query failed: {err.Value.Message}"
};

Console.WriteLine(message);
```

This is the **default** generator output — errors are explicit in the return type, so the compiler makes you handle both the success and failure branches. Prefer a different shape (thrown exceptions, `Task<T>`, a custom result type)? See [Customising generated code](/docs/dataprovider/#customising-generated-code).

## Next Steps

- [Quick Start](/docs/quick-start/) — shorter example focused on the generated API
- [Installation](/docs/installation/) — full package reference
- [Clinical Coding Platform](https://github.com/Nimblesite/ClinicalCoding) — see every tool used in a real multi-service reference implementation
- [LQL](/docs/lql/) — the Lambda Query Language in depth
