# Plan: cross-backend vector / embedding support in DataProviderMigrate + DataProvider codegen

> Implements spec section [MIG-TYPES-VECTOR] in `docs/specs/migration-spec.md`. Target release: **0.9.0-beta**.
>
> **RIGID OWNER RULE:** vector columns MUST work — storage, retrieval, similarity search, index acceleration — on **every** supported backend (PostgreSQL, SQLite, SQL Server). No "graceful fallback to bytes". A backend that cannot host vectors is not a supported backend.

## Why

Applications using semantic search need a single `float[]` C# type driving the same YAML column across Postgres production, on-device SQLite replicas, and SQL Server enterprise. Storing as `text` + query-time casts is unacceptable.

## Backends & hosting

| Backend | Column | How it works | Minimum version |
|---|---|---|---|
| PostgreSQL | `vector(N)` | [pgvector](https://github.com/pgvector/pgvector) extension | any PG ≥ 12, pgvector installed in the DB |
| SQLite | `FLOAT[N]` inside a `vec0` virtual table | [sqlite-vec](https://github.com/asg017/sqlite-vec) extension, loaded via `LoadExtension("vec0")` at connection open | SQLite 3.41+ (sqlite-vec requirement) |
| SQL Server | `VECTOR(N)` native | Built-in type + `VECTOR_DISTANCE()` function | SQL Server 2025 GA or Azure SQL Database |

## Scope (in 0.9.0-beta)

### Core (shared across backends)

| Item | Spec ref | Owner |
|---|---|---|
| `VectorType(int Dimensions)` portable type record | [MIG-TYPES-VECTOR] §5.4 | `lql-cli-merger` |
| YAML `type: Vector(N)` inline-parenthetical parser + writer | §5.4.1 | `lql-cli-merger` |
| `SchemaTypes.IndexDefinition` extended with `IndexType` (enum: `BTree \| IvfflatVector \| HnswVector`) + `VectorOps` (`Cosine \| L2 \| Ip`) + options union | §5.4.3 | `DataProvider` |
| Diagnostics `MIG-E-VECTOR-*` + `MIG-W-VECTOR-*` + `DPSG-VEC-EMPTY` | §5.4.8 | split per owner |

### PostgreSQL backend

| Item | Spec ref | Owner |
|---|---|---|
| `PostgresDdlGenerator.PortableTypeToPostgres` → `VectorType(n) => "vector(n)"` | §5.4.2 | `lql-cli-merger` |
| `PostgresDdlGenerator` prepends `CREATE EXTENSION IF NOT EXISTS vector;` once per DDL batch containing any vector column | §5.4.2 | `lql-cli-merger` |
| `PostgresSchemaInspector` joins `pg_attribute` + decodes `atttypmod` for vector columns; emits `VectorType(dims)` | §5.4.4 | `DataProvider` |
| `PostgresDdlGenerator.GenerateCreateIndex` emits `USING ivfflat (col vector_cosine_ops) WITH (lists = N)` and `USING hnsw (col vector_l2_ops) WITH (m = M, ef_construction = EF)` | §5.4.3 | `DataProvider` |
| `Pgvector.Npgsql` added to `DataProvider/DataProvider/DataProvider.csproj` so introspection resolves the vector type | §5.4.5 | `DataProvider` |
| Postgres codegen emits `float[]` with `Pgvector.Vector` reader/writer binder | §5.4.5 | `DataProvider` |

### SQLite backend (sqlite-vec)

| Item | Spec ref | Owner |
|---|---|---|
| Vendor sqlite-vec native binaries (`vec0.{dll,so,dylib}`) for `win-x64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64` into `DataProviderMigrate` package under `runtimes/{rid}/native/` | §5.4.6 | `DataProvider` (with help from `lql-cli-merger` for package layout) |
| `SqliteDdlGenerator` **does NOT** lower vector columns to BLOB. Instead, split a `VectorType`-bearing table into (a) a regular CREATE TABLE without the vector column(s) and (b) one `CREATE VIRTUAL TABLE "{T}__vec_{col}" USING vec0(rowid INTEGER PRIMARY KEY, embedding FLOAT[N])` per vector column | §5.4.6 | `lql-cli-merger` |
| Auto-generated triggers on the base table for `INSERT`/`DELETE`/`UPDATE` that keep the `T__vec_{col}` virtual table in sync by rowid | §5.4.6 | `lql-cli-merger` |
| `SqliteMigrationRunner` opens connections with `EnableExtensions = true` and calls `LoadExtension("vec0")` before any vector-bearing schema operation. `MIG-E-VECTOR-SQLITE-LOAD` on failure | §5.4.6 | `DataProvider` |
| `SqliteSchemaInspector` reads `PRAGMA table_xinfo("T__vec_{col}")`, parses the `FLOAT[N]` declaration back to `VectorType(N)`, and re-attaches the vector column to the parent table's `DatabaseTable.Columns` before returning | §5.4.6 | `DataProvider` |
| SQLite codegen emits `float[]` via `MemoryMarshal.Cast<byte,float>` from the `vec0` blob read path; writer path goes through the virtual table | §5.4.5, §5.4.6 | `DataProvider` |
| `index_type: ivfflat` / `hnsw` on SQLite — accept the YAML but treat `vec0`'s default ANN as the index; ignore `lists`/`m`/`ef_construction` silently | §5.4.6 | `lql-cli-merger` |

### SQL Server backend (native VECTOR)

| Item | Spec ref | Owner |
|---|---|---|
| `SqlServerDdlGenerator.PortableTypeToSqlServer` → `VectorType(n) => "VECTOR(n)"` | §5.4.7 | `lql-cli-merger` |
| Probe `SERVERPROPERTY('ProductMajorVersion')` before emitting any vector DDL; fail with `MIG-E-VECTOR-MSSQL-VERSION` if server is older than SQL Server 2025 | §5.4.7 | `DataProvider` |
| `SqlServerSchemaInspector` reads `sys.types` for the native vector type and maps back to `VectorType(N)` (reading dimension from `sys.columns.max_length` / `system_type_id` per Microsoft's final GA representation) | §5.4.7 | `DataProvider` |
| `SqlServerDdlGenerator.GenerateCreateIndex` on a vector column with `index_type: ivfflat`/`hnsw` emits a B-Tree index + `MIG-W-VECTOR-MSSQL-ANN` warning (no ANN in GA) | §5.4.7 | `DataProvider` |
| SQL Server codegen uses `SqlDbType.Vector` for the parameter + `reader.GetFieldValue<float[]>` for the reader. Requires `Microsoft.Data.SqlClient` version that exposes `SqlDbType.Vector` | §5.4.5 | `DataProvider` |
| Update `DataProvider/DataProvider/DataProvider.csproj` to pin `Microsoft.Data.SqlClient` to the minimum GA version supporting `SqlDbType.Vector` | §5.4.5 | `DataProvider` |

### Tests (owned by `dataprovider-ci-prep`)

| Item | Spec ref |
|---|---|
| `SchemaYamlSerializerTests` — YAML round-trip `Vector(384)` + negative tests for bare `Vector`, out-of-range dimension | §5.4.1 |
| Switch `PostgresContainerFixture` image to `pgvector/pgvector:pg16` | §5.4.2 |
| Postgres E2E: `Vector_CreateTable_EmitsExtension`, `Vector_CreateTable_RoundTripsDimensions`, `Vector_IvfflatIndex_BuildsAndQueries`, `Vector_HnswIndex_BuildsAndQueries`, `Vector_InspectTable_ReturnsVectorType`, `Vector_CosineDistance_OrdersCorrectly` | §5.4.2–§5.4.4 |
| SQLite E2E: `SqliteVec_Loads`, `Vector_CreateTable_CreatesVec0VirtualTable`, `Vector_Insert_PopulatesVec0`, `Vector_Update_PropagatesToVec0`, `Vector_Delete_RemovesFromVec0`, `Vector_CosineDistance_OrdersCorrectly`, `Vector_InspectTable_ReturnsVectorType` | §5.4.6 |
| SQL Server E2E (if Azure SQL Edge or 2025 preview container available in CI): `Vector_CreateTable_NativeType`, `Vector_VectorDistance_OrdersCorrectly`, `Vector_InspectTable_ReturnsVectorType`. If no testcontainer is available in CI, gate these tests behind `[Fact(Skip = "MIG-TEST-SKIP-MSSQL25")]` with a tracking ticket — **no other skips allowed**. | §5.4.7 |
| DataProvider codegen: `Vector_Codegen_EmitsFloatArray_Postgres`, `_SQLite`, `_SqlServer` | §5.4.5 |
| Codegen negative: `Vector_BindEmptyArray_DPSG_VEC_EMPTY` | §5.4.8 |
| `coverage-thresholds.json` not reduced | — |
| `make ci` green across full matrix on all three backends | — |

## Out of scope (deferred to 0.10.0-beta)

- LQL `cosine_distance(col, @q)` / `l2_distance` / `inner_product` in `order_by` + `limit(@k)` sugar in LQL transpile for all three backends. Tracked as [LQL-COSINE-DISTANCE] in `lql-spec.md` (pending).
- `ReadOnlyMemory<float>` codegen variant (`--vector-repr=readonly-memory`).
- Half-precision (`halfvec`), sparse (`sparsevec`), bit-packed vectors.
- Vector-aware sync diff (Sync framework treats vectors as opaque bytes for now).
- pgvector HNSW/IVFFlat tuning beyond `lists`/`m`/`ef_construction` (e.g. `ef_search` runtime tweak).
- SQL Server ANN index support (waiting on Microsoft).

## Execution order (strict)

1. **Spec + plan landed** (migration-spec.md §5.4 + this file). **Done by DataProvider.**
2. **`lql-cli-merger` core**: `VectorType(int Dimensions)` in `PortableTypes.cs`, YAML r/w in `SchemaYamlSerializer.cs`. No tests in this step.
3. **`lql-cli-merger` Postgres + SQLite + SQL Server DDL**: PG `vector(N)` + extension prologue, SQLite `vec0` virtual table split + triggers, SQL Server `VECTOR(n)`. Still no tests.
4. **`DataProvider` shared SchemaTypes**: extend `IndexDefinition` with `IndexType` + `VectorOps` + options union. Lock `SchemaTypes.cs` briefly, land, release.
5. **`DataProvider` per-backend inspectors**: Postgres (`atttypmod` decode), SQLite (`PRAGMA table_xinfo` on `__vec_` VT, reattach to parent `DatabaseTable`), SQL Server (`sys.types`/`sys.columns` decode).
6. **`DataProvider` index DDL**: extend `PostgresDdlGenerator.GenerateCreateIndex` (ivfflat/hnsw), `SqliteDdlGenerator.GenerateCreateIndex` (accept + ignore options), `SqlServerDdlGenerator.GenerateCreateIndex` (B-Tree + warning).
7. **`DataProvider` runtime**: `Pgvector.Npgsql` dep on the DataProvider tool csproj; `Microsoft.Data.SqlClient` version bump; sqlite-vec native binaries vendored under `runtimes/{rid}/native/` of the DataProviderMigrate tool package; `SqliteMigrationRunner` loads the extension at connection open.
8. **`DataProvider` codegen**: `PostgresCli.cs`, `SqliteCli.cs`, `SqlServerCli.cs` all emit `float[]` in records, readers, binders.
9. **`dataprovider-ci-prep`**: failing tests first (CLAUDE.md process), then implementation validation, then `make ci`. Bump `PostgresContainerFixture` to `pgvector/pgvector:pg16`.
10. **`dataprovider-ci-prep`** (last): version bump to `0.9.0-beta` across every csproj + `Directory.Build.props`. Final `make ci` run.
11. **Owner**: `git tag v0.9.0-beta` → `release.yml` triggers and publishes all packages.
12. **[ClinicalCoding](https://github.com/Nimblesite/ClinicalCoding)**: pin to 0.9.0-beta, convert `text` embedding columns to `Vector(384)`, rewrite their LQL to use `cosine_distance` (pending 0.10.0-beta — in the meantime they emit raw similarity SQL via LQL passthrough).

## File inventory (exact, by owner)

### `lql-cli-merger`

- [Migration/Nimblesite.DataProvider.Migration.Core/PortableTypes.cs](Migration/Nimblesite.DataProvider.Migration.Core/PortableTypes.cs) — add `public sealed record VectorType(int Dimensions) : PortableType;`.
- [Migration/Nimblesite.DataProvider.Migration.Core/SchemaYamlSerializer.cs](Migration/Nimblesite.DataProvider.Migration.Core/SchemaYamlSerializer.cs) — extend parser + writer for `Vector(N)`. Reject bare `Vector` with `MIG-E-VECTOR-DIMS-MISSING`, out-of-range with `MIG-E-VECTOR-DIMS-RANGE`.
- [Migration/Nimblesite.DataProvider.Migration.Postgres/PostgresDdlGenerator.cs](Migration/Nimblesite.DataProvider.Migration.Postgres/PostgresDdlGenerator.cs) — `VectorType(n) => "vector(n)"` in `PortableTypeToPostgres`; prepend `CREATE EXTENSION IF NOT EXISTS vector;` once per DDL batch that contains any vector column.
- [Migration/Nimblesite.DataProvider.Migration.SQLite/SqliteDdlGenerator.cs](Migration/Nimblesite.DataProvider.Migration.SQLite/SqliteDdlGenerator.cs) — **replace the BLOB fallback plan**: emit `vec0` virtual table split + triggers per §5.4.6. Auto-generated trigger SQL follows.
- [Migration/Nimblesite.DataProvider.Migration.SqlServer/SqlServerDdlGenerator.cs](Migration/Nimblesite.DataProvider.Migration.SqlServer/SqlServerDdlGenerator.cs) — `VectorType(n) => "VECTOR(n)"` in `PortableTypeToSqlServer`.

### `DataProvider` (me)

- [Migration/Nimblesite.DataProvider.Migration.Core/SchemaTypes.cs](Migration/Nimblesite.DataProvider.Migration.Core/SchemaTypes.cs) — extend `IndexDefinition` with `IndexType`, `VectorOps`, index options union.
- [Migration/Nimblesite.DataProvider.Migration.Postgres/PostgresSchemaInspector.cs](Migration/Nimblesite.DataProvider.Migration.Postgres/PostgresSchemaInspector.cs) — join `pg_attribute`, decode `atttypmod` for vector columns, map to `VectorType(dims)`. `MIG-E-VECTOR-PG-INTROSPECT` on decode failure.
- [Migration/Nimblesite.DataProvider.Migration.Postgres/PostgresDdlGenerator.cs](Migration/Nimblesite.DataProvider.Migration.Postgres/PostgresDdlGenerator.cs) (shared with lql-cli-merger — lock briefly) — extend `GenerateCreateIndex` for `ivfflat`/`hnsw` + `vector_ops` + options. Validate `MIG-E-VECTOR-IDX-OPTIONS` / `MIG-E-VECTOR-IDX-NONVECTOR`.
- [Migration/Nimblesite.DataProvider.Migration.SQLite/SqliteSchemaInspector.cs](Migration/Nimblesite.DataProvider.Migration.SQLite/SqliteSchemaInspector.cs) — discover `{T}__vec_{col}` virtual tables, parse `FLOAT[N]` from `PRAGMA table_xinfo`, re-attach the vector column to the parent `DatabaseTable`.
- [Migration/Nimblesite.DataProvider.Migration.SQLite/SqliteMigrationRunner.cs](Migration/Nimblesite.DataProvider.Migration.SQLite/SqliteMigrationRunner.cs) — set `EnableExtensions = true` and `LoadExtension("vec0")` when the schema contains any vector column. `MIG-E-VECTOR-SQLITE-LOAD` on failure.
- [Migration/Nimblesite.DataProvider.Migration.SqlServer/SqlServerSchemaInspector.cs](Migration/Nimblesite.DataProvider.Migration.SqlServer/SqlServerSchemaInspector.cs) — `sys.types`/`sys.columns` read for native `VECTOR(N)`. Version gate via `SERVERPROPERTY('ProductMajorVersion')`. `MIG-E-VECTOR-MSSQL-VERSION` if too old.
- [Migration/Nimblesite.DataProvider.Migration.SqlServer/SqlServerDdlGenerator.cs](Migration/Nimblesite.DataProvider.Migration.SqlServer/SqlServerDdlGenerator.cs) (shared with lql-cli-merger — lock briefly) — index path emits B-Tree + `MIG-W-VECTOR-MSSQL-ANN` warning.
- [DataProvider/DataProvider/DataProvider.csproj](DataProvider/DataProvider/DataProvider.csproj) — add `<PackageReference Include="Pgvector.Npgsql" Version="*" />`; pin `Microsoft.Data.SqlClient` to a version exposing `SqlDbType.Vector`.
- [DataProvider/DataProvider/PostgresCli.cs](DataProvider/DataProvider/PostgresCli.cs) — `VectorType(var n)` case → C# `float[]`; reader via `Pgvector.Vector`; writer via `new Pgvector.Vector(arr)`; `float[]?` when nullable.
- [DataProvider/DataProvider/SqliteCli.cs](DataProvider/DataProvider/SqliteCli.cs) — `VectorType(var n)` case → `float[]`; reader via `MemoryMarshal.Cast<byte,float>` from the `vec0` blob; writer targets `{T}__vec_{col}` via `MemoryMarshal.AsBytes`.
- [DataProvider/DataProvider/SqlServerCli.cs](DataProvider/DataProvider/SqlServerCli.cs) — `VectorType(var n)` case → `float[]`; reader `GetFieldValue<float[]>`; writer `SqlDbType.Vector`.
- **sqlite-vec native binaries**: vendored into `DataProvider/DataProvider/runtimes/{rid}/native/` for the 5 RIDs, plus a build target that copies them into the final tool package on `dotnet pack`. Sources: GitHub release tarballs at [`asg017/sqlite-vec`](https://github.com/asg017/sqlite-vec/releases), license MIT. Checksums recorded in `DataProvider/DataProvider/sqlite-vec.checksums.txt`.

### `dataprovider-ci-prep`

- [Migration/Nimblesite.DataProvider.Migration.Tests/SchemaYamlSerializerTests.cs](Migration/Nimblesite.DataProvider.Migration.Tests/SchemaYamlSerializerTests.cs) — `Vector(384)` round-trip + `Vector_BareTypeFails` + `Vector_DimensionOutOfRange`.
- [Migration/Nimblesite.DataProvider.Migration.Tests/PostgresContainerFixture.cs](Migration/Nimblesite.DataProvider.Migration.Tests/PostgresContainerFixture.cs) — switch image to `pgvector/pgvector:pg16`.
- [Migration/Nimblesite.DataProvider.Migration.Tests/PostgresMigrationTests.cs](Migration/Nimblesite.DataProvider.Migration.Tests/PostgresMigrationTests.cs) — new vector tests per "Tests" section above.
- [Migration/Nimblesite.DataProvider.Migration.Tests/SqliteMigrationTests.cs](Migration/Nimblesite.DataProvider.Migration.Tests/SqliteMigrationTests.cs) — `SqliteVec_Loads`, full round-trip tests via `vec0`, similarity search assertions.
- [Migration/Nimblesite.DataProvider.Migration.Tests/SqlServerMigrationTests.cs](Migration/Nimblesite.DataProvider.Migration.Tests/SqlServerMigrationTests.cs) — native `VECTOR(N)` round-trip + `VECTOR_DISTANCE` ordering. Gate with `Skip="MIG-TEST-SKIP-MSSQL25"` iff CI has no access to SQL Server 2025.
- `DataProvider.Tests` — per-backend codegen tests.
- `coverage-thresholds.json` — do not drop thresholds.
- Final bump: every csproj + `Directory.Build.props` to `0.9.0-beta`.

## Coordination rules

- **Shared files** (`PostgresDdlGenerator.cs`, `SqlServerDdlGenerator.cs`): acquire TMC lock, do the edit, release immediately. Never hold.
- All three agents share the `GeneratorTool` branch worktree. Commit messages reference `[MIG-TYPES-VECTOR]`, `[MIG-TYPES-VECTOR-SQLITE]`, `[MIG-TYPES-VECTOR-MSSQL]`, or `[DP-CODEGEN-VECTOR]`.
- CLAUDE.md bug-fix / feature process is MANDATORY: failing test first, verify it fails, implement, verify it passes. No exceptions.

## Rollback

If the cross-backend vector work cannot go green in 0.9.0-beta:

1. `git revert` the vector commits.
2. Ship 0.9.0-beta as a pure `Lql`-rename + BUG-fixes release (no vector).
3. Push vector to 0.10.0-beta.
4. ClinicalCoding continues with text+cast until 0.10.0-beta.

**No partial vector rollout.** Either all three backends work or none ship. A half-vector 0.9.0-beta that works on Postgres but not SQLite violates the owner rule.
