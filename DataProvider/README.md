# DataProvider

A build-time CLI code generator for .NET that creates compile-time safe database extension methods from SQL and LQL query files. Every generated method returns `Result<T, SqlError>` — no exceptions, no reflection, no runtime overhead.

Supports **SQLite**, **PostgreSQL**, and **SQL Server**.

## How it works

DataProvider is a **dotnet CLI tool**, not a Roslyn analyzer. It runs during the build, reads your SQL/LQL files plus a `DataProvider.json` manifest, and emits `.g.cs` files that your project compiles normally. The three tools form a pipeline:

```mermaid
flowchart LR
    Yaml["example-schema.yaml"] -->|DataProviderMigrate| Db["invoices.db"]
    Lql["GetCustomers.lql"] -->|Lql| Sql["GetCustomers.generated.sql"]
    Config["DataProvider.json"] --> Gen["DataProvider"]
    Sql --> Gen
    Gen --> Cs["Generated/*.g.cs"]
```

## Install

```bash
dotnet new tool-manifest
dotnet tool install DataProvider --version ${DATAPROVIDER_VERSION}
dotnet add package Nimblesite.DataProvider.SQLite --version ${NIMBLESITE_VERSION}
```

Replace `SQLite` with `Postgres` or `SqlServer` as needed.

## Runtime packages

| Package | Purpose |
|---------|---------|
| `Nimblesite.DataProvider.Core` | Shared runtime types (`Result<T,E>`, `SqlError`) |
| `Nimblesite.DataProvider.SQLite` | SQLite runtime |
| `Nimblesite.DataProvider.Postgres` | PostgreSQL runtime |
| `Nimblesite.DataProvider.SqlServer` | SQL Server runtime |

## DataProvider.json

Describes what to generate from your SQL/LQL files and tables:

```json
{
  "connectionString": "Data Source=app.db",
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
      "primaryKeyColumns": ["Id"],
      "generateInsert": true,
      "generateUpdate": true,
      "generateDelete": true
    }
  ]
}
```

## Running the generator

Manually:

```bash
dotnet DataProvider sqlite --project-dir . --config DataProvider.json --out ./Generated
dotnet DataProvider postgres --project-dir . --config DataProvider.json --out ./Generated --connection-string "Host=localhost;Database=mydb;..."
```

Or wire it into MSBuild so every build regenerates code:

```xml
<Target Name="RunDataProvider" BeforeTargets="CoreCompile">
  <Exec Command="dotnet DataProvider sqlite --project-dir . --config DataProvider.json --out ./Generated" />
  <ItemGroup>
    <Compile Include="Generated/**/*.g.cs" />
  </ItemGroup>
</Target>
```

## Using generated methods (default template)

Out of the box, the generator emits methods that make errors explicit in the return type — no thrown exceptions on the query path:

```csharp
using Microsoft.Data.Sqlite;
using Nimblesite.DataProvider.Core;
using MyApp.Generated;

await using var connection = new SqliteConnection("Data Source=app.db");
await connection.OpenAsync();

var result = await connection.GetCustomersAsync(customerId: null);

switch (result)
{
    case Result<IReadOnlyList<GetCustomersRow>, SqlError>.Ok ok:
        foreach (var customer in ok.Value)
            Console.WriteLine($"{customer.Id}: {customer.CustomerName}");
        break;

    case Result<IReadOnlyList<GetCustomersRow>, SqlError>.Error err:
        Console.Error.WriteLine($"Query failed: {err.Value.Message}");
        break;
}
```

Generated row types are immutable records. Generated insert/update/delete methods follow the same default shape.

## Customising generated code

The default output shape is **fully pluggable**. The code generator is driven by a `CodeGenerationConfig` record that holds a set of `Func<>` delegates — one per piece of the emitted code. Swap any of them and the generator emits whatever you want: raw `Task<T>`, `Option<T>`, thrown exceptions, custom result types, bespoke naming conventions, you name it.

Key extension points (see `Nimblesite.DataProvider.Core.CodeGeneration.CodeGenerationConfig`):

| Delegate | What it controls |
|---|---|
| `GenerateDataAccessMethod` | The signature and body of each query method (this is where you change the return type) |
| `GenerateModelType` | How row/parameter records are emitted |
| `GenerateGroupedModels` | How nested grouping models (parent + child collections) are emitted |
| `GenerateSourceFile` | The overall file layout (`using`s, namespace, class wrapper) |

Minimal custom template (programmatic):

```csharp
using Nimblesite.DataProvider.Core.CodeGeneration;

var config = new CodeGenerationConfig(getColumnMetadata, tableOpGenerator)
{
    // Emit methods that throw instead of returning Result<T, SqlError>
    GenerateDataAccessMethod = (className, methodName, sql, parameters, columns, connType) =>
        $$"""
        public static async Task<IReadOnlyList<{{className}}Row>> {{methodName}}Async(
            this {{connType}} connection /* … */)
        {
            // your bespoke body
        }
        """
};

// Pass the config to the platform-specific generator
SqliteCodeGenerator.GenerateCodeWithMetadata(config: config, /* … */);
```

> **Today this hook is programmatic-only** — custom templates are wired up in code that drives the generator library directly. The `DataProvider` CLI does not yet accept a `--template` flag or a `template` field in `DataProvider.json`; support for CLI-level template selection is tracked as future work. If you need a custom template right now, reference `Nimblesite.DataProvider.Core` from a small generator project and call `GenerateCodeWithMetadata` yourself.

## Related

- [LQL](../Lql/README.md) — cross-database query language that transpiles to SQL
- [Migrations](../Migration/README.md) — YAML schema definitions consumed by `DataProviderMigrate`
- Migration spec: [docs/specs/migration-spec.md](../docs/specs/migration-spec.md#74-dataprovidermigrate-cli-mig-cli)
