# DataProvider Suite

DataProvider is a complete toolkit for .NET database access that prioritises **type safety**. It provides CLI-driven source generation for SQL extension methods, a cross-database query language (LQL), offline-first bidirectional sync, and YAML schema migrations.

## Philosophy

DataProvider fixes issues that have plagued .NET data access for decades.

**The simplicity and safety of an ORM without the downsides.** DataProvider generates extension methods directly from your SQL or LQL files. You write the queries. You see what executes. SQL errors become compilation errors. No magic, no reflection.

**Errors you can see.** Database operations fail. Networks drop. Constraints get violated. These are expected, not exceptional. DataProvider makes error handling explicit so failures surface in your types rather than hiding in catch blocks. The default output shape is fully customisable — swap the code template to fit your project's conventions.

**SQL is the source of truth.** Define schemas in YAML, write queries in SQL or LQL, and generate strongly-typed code from both.

**Sync-native.** Run occasionally connected apps or synchronise data across microservices. Conflict resolution, tombstones, and subscriptions included.

## The Stack

| Component | Purpose |
|-----------|---------|
| [DataProvider](./DataProvider/README.md) | CLI source generator: SQL/LQL files become type-safe extension methods |
| [LQL](./Lql/README.md) | Lambda Query Language: write once, transpile to any SQL dialect |
| [Migrations](./Migration/README.md) | YAML schemas: database-agnostic, version-controlled schema definitions |
| [Sync](./Sync/README.md) | Offline-first: bidirectional synchronisation with conflict resolution |

Each component works independently or together. Use what you need.

## Installation

DataProvider ships in two halves — three **CLI tools** that generate code at build time, and **runtime library packages** that your app references.

```bash
# 1. CLI tools (pinned in .config/dotnet-tools.json)
dotnet new tool-manifest
dotnet tool install DataProvider --version ${DATAPROVIDER_VERSION}
dotnet tool install DataProviderMigrate --version ${DATAPROVIDERMIGRATE_VERSION}
dotnet tool install Lql --version ${LQL_VERSION}

# 2. Runtime packages (pick your database)
dotnet add package Nimblesite.DataProvider.SQLite --version ${NIMBLESITE_VERSION}
# or: Nimblesite.DataProvider.Postgres / Nimblesite.DataProvider.SqlServer
```

See the [installation guide](./Website/src/docs/installation.md) for the full package list, including `Nimblesite.Lql.*`, `Nimblesite.Sync.*`, and `Nimblesite.Reporting.Engine`.

## Quick Example

![alt text](lql.png)

Write an LQL query in `GetActiveCustomers.lql`:

```
Customer
|> filter(fn(row) => Customer.IsActive = true)
|> join(Address, on = Customer.Id = Address.CustomerId)
|> select(Customer.Id, Customer.Name, Address.City)
|> limit(100)
```

Run the CLI tools (typically wired into MSBuild targets):

```bash
dotnet Lql sqlite --input GetActiveCustomers.lql --output GetActiveCustomers.generated.sql
dotnet DataProvider sqlite --project-dir . --config DataProvider.json --out ./Generated
```

Call the generated extension method with exhaustive error handling:

```csharp
var result = await connection.GetActiveCustomersAsync();

var message = result switch
{
    Result<IReadOnlyList<GetActiveCustomersRow>, SqlError>.Ok ok =>
        $"Found {ok.Value.Count} customers",
    Result<IReadOnlyList<GetActiveCustomersRow>, SqlError>.Error err =>
        $"Failed: {err.Value.Message}"
};
```

The shape above is the **default** emitted template. Want `Task<T>`, a thrown exception, an `Option<T>`, or your own custom signature? Plug in a custom code template — see the [DataProvider docs](./DataProvider/README.md#customising-generated-code).

## Reference Implementation

The **[Nimblesite Clinical Coding Platform](https://github.com/Nimblesite/ClinicalCoding)** is the canonical reference implementation — a full-stack healthcare app built on .NET 10, PostgreSQL + pgvector, FHIR R5, and every component of this toolkit. Not for production; technology demonstration only.

## Prerequisites

- .NET 10 SDK
- A database (SQLite, PostgreSQL, or SQL Server)

## Build from Source

```bash
git clone https://github.com/Nimblesite/DataProvider.git
cd DataProvider
make ci            # lint + test + build
```

## Performance

- **Zero runtime overhead** — generated code is pure ADO.NET
- **AOT compatible** — full ahead-of-time compilation support
- **No reflection** — all code generated at compile time
- **Minimal allocations** — optimised for low memory usage

## Contributing

See [CLAUDE.md](CLAUDE.md) for code style, architecture rules, and testing requirements. Log an issue or start a discussion before submitting non-trivial PRs.

## License

MIT © 2026 Nimblesite Pty Ltd. See [LICENSE](./LICENSE).
