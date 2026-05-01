# Plan: Row-Level Security (RLS) Migration Abstraction

> Implements spec `docs/specs/rls-spec.md` (sections `[RLS-*]`).

## Context

Multi-tenant and healthcare applications need row-level access control enforced at the database layer. PostgreSQL and SQL Server have native RLS; SQLite does not. This plan adds a platform-independent RLS abstraction to the Migration framework so a single YAML policy definition produces correct enforcement on every backend -- native `CREATE POLICY` on Postgres, `CREATE SECURITY POLICY` on SQL Server, and trigger-based emulation on SQLite.

Predicates that query other tables (e.g. group membership) MUST use LQL, transpiled to platform SQL at migration time.

## Scope

- Core types: `RlsPolicySetDefinition`, `RlsPolicyDefinition`, `RlsOperation` enum
- Schema operations: `EnableRlsOperation`, `CreateRlsPolicyOperation`, `DropRlsPolicyOperation`, `DisableRlsOperation`
- LQL predicate transpiler: `current_user_id()` substitution + `exists()` subquery delegation per platform
- PostgreSQL DDL: native `CREATE POLICY` / `ALTER TABLE ... ENABLE ROW LEVEL SECURITY`
- SQLite DDL: `__rls_context` table + `BEFORE INSERT/UPDATE/DELETE` triggers + `_secure` views
- YAML serialization round-trip
- Schema diff for RLS changes
- Schema inspector extensions (Postgres `pg_policies`, SQLite `rls_*` trigger reverse-mapping)
- SQL Server: deferred (package does not exist yet) -- emit `MIG-E-RLS-MSSQL-UNSUPPORTED`

## New Files

| File | Purpose |
|---|---|
| `Migration/Nimblesite.DataProvider.Migration.Core/RlsDefinition.cs` | `RlsPolicySetDefinition`, `RlsPolicyDefinition`, `RlsOperation` enum |
| `Migration/Nimblesite.DataProvider.Migration.Core/RlsPredicateTranspiler.cs` | `current_user_id()` substitution + LQL subquery delegation per platform |
| `Migration/Nimblesite.DataProvider.Migration.Tests/RlsPredicateTranspilerTests.cs` | Transpiler unit tests |

## Modified Files

| File | Change |
|---|---|
| [SchemaDefinition.cs](Migration/Nimblesite.DataProvider.Migration.Core/SchemaDefinition.cs) | Add `RlsPolicySetDefinition? RowLevelSecurity` to `TableDefinition` |
| [SchemaOperation.cs](Migration/Nimblesite.DataProvider.Migration.Core/SchemaOperation.cs) | Add 4 RLS operation records |
| [DdlGenerator.cs](Migration/Nimblesite.DataProvider.Migration.Core/DdlGenerator.cs) | `IsDestructive` includes `DropRlsPolicyOperation`, `DisableRlsOperation` |
| [SchemaDiff.cs](Migration/Nimblesite.DataProvider.Migration.Core/SchemaDiff.cs) | RLS diff logic |
| [SchemaYamlSerializer.cs](Migration/Nimblesite.DataProvider.Migration.Core/SchemaYamlSerializer.cs) | Converters + type mappings for RLS types |
| [Nimblesite.DataProvider.Migration.Core.csproj](Migration/Nimblesite.DataProvider.Migration.Core/Nimblesite.DataProvider.Migration.Core.csproj) | `ProjectReference` to `Nimblesite.Lql.Core`, `.Postgres`, `.SQLite`, `.SqlServer` |
| [PostgresDdlGenerator.cs](Migration/Nimblesite.DataProvider.Migration.Postgres/PostgresDdlGenerator.cs) | Handle 4 RLS operations with native DDL |
| [PostgresSchemaInspector.cs](Migration/Nimblesite.DataProvider.Migration.Postgres/PostgresSchemaInspector.cs) | Read `pg_policies` into `RlsPolicySetDefinition` |
| [SqliteDdlGenerator.cs](Migration/Nimblesite.DataProvider.Migration.SQLite/SqliteDdlGenerator.cs) | `__rls_context` + triggers + `_secure` views |
| [SqliteSchemaInspector.cs](Migration/Nimblesite.DataProvider.Migration.SQLite/SqliteSchemaInspector.cs) | Reverse-map `rls_*` triggers |
| [PostgresMigrationTests.cs](Migration/Nimblesite.DataProvider.Migration.Tests/PostgresMigrationTests.cs) | Postgres RLS E2E tests |
| [SqliteMigrationTests.cs](Migration/Nimblesite.DataProvider.Migration.Tests/SqliteMigrationTests.cs) | SQLite trigger RLS E2E tests |
| [SchemaYamlSerializerTests.cs](Migration/Nimblesite.DataProvider.Migration.Tests/SchemaYamlSerializerTests.cs) | YAML round-trip tests |

## Execution Order

1. Core types: `RlsDefinition.cs` (new), extend `SchemaDefinition.cs`, `SchemaOperation.cs`, `DdlGenerator.cs`
2. YAML: `SchemaYamlSerializer.cs` converters + type mappings, then YAML round-trip tests
3. `RlsPredicateTranspiler.cs` with `current_user_id()` per-platform substitution, then unit tests
4. LQL subquery integration: wire LQL project references into `Migration.Core.csproj`, then subquery tests
5. `PostgresDdlGenerator.cs`: RLS operation handlers, then DDL string assertion tests
6. Postgres E2E tests (failing first, then implementation validates)
7. `PostgresSchemaInspector.cs`: read `pg_policies`, then inspector round-trip tests
8. `SchemaDiff.cs`: RLS diff logic, then diff assertion tests
9. `SqliteDdlGenerator.cs`: `__rls_context` + triggers + views, then DDL string assertion tests
10. SQLite E2E tests (failing first, then implementation validates)
11. `SqliteSchemaInspector.cs`: reverse-map triggers, then inspector round-trip tests
12. SQL Server placeholder: `MIG-E-RLS-MSSQL-UNSUPPORTED` error guard
13. `make ci` green across all backends

## Verification

- `make test` -- all new and existing tests pass
- `make ci` -- full CI simulation green
- Coverage thresholds maintained
- Manual: create a schema YAML with RLS policies, run `DataProviderMigrate` against Postgres and SQLite, verify policies/triggers are created, verify cross-user access is blocked

---

## TODO

- [ ] Create `RlsPolicySetDefinition`, `RlsPolicyDefinition`, `RlsOperation` in new `Migration/Nimblesite.DataProvider.Migration.Core/RlsDefinition.cs`
- [ ] Add `RlsPolicySetDefinition? RowLevelSecurity` property to `TableDefinition` in `SchemaDefinition.cs`
- [ ] Add `EnableRlsOperation`, `CreateRlsPolicyOperation`, `DropRlsPolicyOperation`, `DisableRlsOperation` to `SchemaOperation.cs`
- [ ] Extend `IsDestructive` in `DdlGenerator.cs` for `DropRlsPolicyOperation` and `DisableRlsOperation`
- [ ] Add YAML converters and type mappings for RLS types in `SchemaYamlSerializer.cs`
- [ ] Write failing YAML round-trip tests in `SchemaYamlSerializerTests.cs`
- [ ] Make YAML round-trip tests pass
- [ ] Create `RlsPredicateTranspiler.cs` with `current_user_id()` per-platform substitution and LQL subquery delegation
- [ ] Add `ProjectReference` entries for `Nimblesite.Lql.Core`, `.Postgres`, `.SQLite`, `.SqlServer` to `Migration.Core.csproj`
- [ ] Write failing `RlsPredicateTranspiler` unit tests in new `RlsPredicateTranspilerTests.cs`
- [ ] Make `RlsPredicateTranspiler` tests pass
- [ ] Implement RLS operation handling in `PostgresDdlGenerator.cs` (Enable, Create, Drop, Disable)
- [ ] Write failing Postgres RLS E2E tests in `PostgresMigrationTests.cs`
- [ ] Extend `PostgresSchemaInspector.cs` to read `pg_policies` into `RlsPolicySetDefinition`
- [ ] Make Postgres E2E tests pass
- [ ] Extend `SchemaDiff.Calculate` in `SchemaDiff.cs` with RLS diff logic
- [ ] Write failing SQLite RLS E2E tests in `SqliteMigrationTests.cs`
- [ ] Implement `__rls_context` table, trigger generation, and `_secure` view generation in `SqliteDdlGenerator.cs`
- [ ] Extend `SqliteSchemaInspector.cs` to reverse-map `rls_*` triggers
- [ ] Make SQLite E2E tests pass
- [ ] Add `MIG-E-RLS-MSSQL-UNSUPPORTED` error guard for SQL Server
- [ ] Run `make ci` -- all tests pass, coverage thresholds maintained
- [ ] Update `Migration/README.md` with RLS usage examples
