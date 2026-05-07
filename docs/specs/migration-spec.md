# .NET Schema Migration Framework Specification

## Table of Contents

1. [Introduction](#1-introduction)
2. [Goals & Non-Goals](#2-goals--non-goals)
3. [Architecture Overview](#3-architecture-overview)
4. [Schema Definition Model](#4-schema-definition-model)
5. [Type System](#5-type-system) — includes [MIG-TYPES-VECTOR] in §5.4
6. [Schema Operations](#6-schema-operations)
7. [Migration Execution](#7-migration-execution)
8. [Diff Engine](#8-diff-engine)
9. [Database Providers](#9-database-providers)
10. [Error Handling](#10-error-handling)
11. [Conformance Requirements](#11-conformance-requirements)
12. [E2E Testing Requirements](#12-e2e-testing-requirements)
13. [Appendices](#13-appendices)

---

## 1. Introduction

This specification defines a database-agnostic schema migration framework for .NET applications. The framework enables declarative schema definitions that can create databases from scratch or upgrade existing databases through additive migrations.

### 1.1 Scope

This specification covers:

- Database-agnostic schema definitions (tables, columns, indexes, keys, triggers, functions, etc.)
- Schema creation from scratch (greenfield deployments)
- Additive schema upgrades (adding columns, tables, indexes)
- Schema introspection and diff calculation
- Platform-specific DDL generation (SQLite, PostgreSQL, SQL Server)

This specification does **not** cover:

- Destructive migrations (dropping columns, tables) - these require explicit opt-in
- Data migrations (transforming existing data)
- Rollback mechanisms (out of scope for v1)

### 1.2 Relationship to Other Frameworks

The Migration framework is **independent** but serves as a foundation for:

- **Sync Framework**: Uses Migration to create sync infrastructure tables (`_sync_log`, `_sync_state`, etc.)
- **DataProvider**: Uses schema introspection for code generation
- **LQL**: Can leverage schema metadata for query validation

---

## 2. Goals & Non-Goals

### 2.1 Goals

- **Database-agnostic definitions**: Single schema definition works across SQLite, PostgreSQL, SQL Server
- **Additive-only by default**: Safe upgrades that only add, never remove
- **Idempotent operations**: Running migrations multiple times produces same result
- **Introspection-first**: Compare desired schema against actual database state
- **Explicit over implicit**: No magic - every operation is visible and auditable
- **Zero dependencies**: Pure .NET, no external migration tools outside this repo. But should use other libraries in this repo.

### 2.2 Non-Goals

- **ORM functionality**: This is schema management only, not data access
- **Automatic rollbacks**: Destructive operations require explicit handling
- **Migration history tables**: Version tracking is application responsibility
- **Complex data transforms**: Use LQL scripts or application code for data migration

---

## 3. Architecture Overview

```
+-----------------------------------------------------------+
|                    Application Layer                       |
+-----------------------------------------------------------+
|                    Migration Engine                        |
|  +-------------+ +-------------+ +-------------+           |
|  |   Schema    | |    Diff     | |   DDL       |           |
|  |  Definition | |   Engine    | | Generator   |           |
|  +-------------+ +-------------+ +-------------+           |
+-----------------------------------------------------------+
|                  Provider Layer                            |
|     +----------+  +----------+  +----------+               |
|     | SQLite   |  | Postgres |  | SqlServer|               |
|     | Provider |  | Provider |  | Provider |               |
|     +----------+  +----------+  +----------+               |
+-----------------------------------------------------------+
|                    Database Layer                          |
|              (SQLite / PostgreSQL / SQL Server)            |
+-----------------------------------------------------------+
```

---

## 4. Schema Definition Model

### 4.1 Core Records

Schema is defined using immutable records. The key types are:

- **SchemaDefinition** - Root container with schema name and list of tables
- **TableDefinition** - Table with columns, indexes, foreign keys, primary key, unique constraints, check constraints, and optional comment
- **ColumnDefinition** - Column with name, portable type, nullable flag, default value, identity settings, computed expression, collation, check constraint, and comment
- **IndexDefinition** - Index with name, columns, unique flag, and optional filter (partial index)
- **ForeignKeyDefinition** - FK with columns, referenced table/columns, and ON DELETE/UPDATE actions
- **PrimaryKeyDefinition** - PK with optional name and column list
- **UniqueConstraintDefinition** - Unique constraint with columns
- **CheckConstraintDefinition** - Check constraint with SQL boolean expression

Foreign key actions: `NoAction`, `Cascade`, `SetNull`, `SetDefault`, `Restrict`

### 4.2 Fluent Builder (Optional)

For ergonomic schema definition:

```csharp
var schema = Schema.Define("MyApp")
    .Table("Person", t => t
        .Column("Id", PortableType.Uuid, c => c.PrimaryKey())
        .Column("Name", PortableType.String(100), c => c.NotNull())
        .Column("Email", PortableType.String(255))
        .Column("CreatedAt", PortableType.DateTime, c => c.NotNull().Default("CURRENT_TIMESTAMP"))
        .Index("idx_person_email", "Email", unique: true)
    )
    .Table("Order", t => t
        .Column("Id", PortableType.Uuid, c => c.PrimaryKey())
        .Column("PersonId", PortableType.Uuid, c => c.NotNull())
        .Column("Total", PortableType.Decimal(10, 2), c => c.NotNull())
        .ForeignKey("PersonId", "Person", "Id", onDelete: ForeignKeyAction.Cascade)
    )
    .Build();
```

### 4.3 YAML Schema Format

Schema files use YAML format. See [migration-cli-spec.md](migration-cli-spec.md) for CLI usage. The YAML format mirrors the C# records:

```yaml
name: MyApp
tables:
  - schema: public
    name: Product
    comment: Product catalog
    columns:
      - name: Id
        type: { kind: bigint }
        nullable: false
        identity: { seed: 1, increment: 1 }
      - name: Sku
        type: { kind: char, length: 12 }
        nullable: false
        comment: Stock keeping unit
      - name: Name
        type: { kind: varchar, maxLength: 200 }
        nullable: false
      - name: Price
        type: { kind: decimal, precision: 10, scale: 2 }
        nullable: false
        default: "0.00"
        checkConstraint: "Price >= 0"
      - name: IsActive
        type: { kind: boolean }
        nullable: false
        default: "true"
    primaryKey:
      name: PK_Product
      columns: [Id]
    indexes:
      - name: IX_Product_Sku
        columns: [Sku]
        unique: true
    foreignKeys: []
```

### 4.4 YAML Type Reference

Type definitions use the `kind` property to discriminate:

#### Types with NO parameters

| Type Kind | Example |
|-----------|---------|
| `tinyint` | `kind: tinyint` |
| `smallint` | `kind: smallint` |
| `int` | `kind: int` |
| `bigint` | `kind: bigint` |
| `float` | `kind: float` |
| `double` | `kind: double` |
| `text` | `kind: text` |
| `blob` | `kind: blob` |
| `date` | `kind: date` |
| `uuid` | `kind: uuid` |
| `boolean` | `kind: boolean` |

#### Types with parameters

| Type Kind | Parameters | Example |
|-----------|------------|---------|
| `char` | `length` | `{ kind: char, length: 10 }` |
| `varchar` | `maxLength` | `{ kind: varchar, maxLength: 255 }` |
| `decimal` | `precision`, `scale` | `{ kind: decimal, precision: 18, scale: 2 }` |
| `datetime` | `precision` | `{ kind: datetime, precision: 3 }` |

### 4.5 Column Property Reference

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `name` | string | (required) | Column name |
| `type` | object | (required) | Type definition (see 4.4) |
| `nullable` | boolean | `true` | Allow NULL values |
| `default` | string | `null` | SQL default expression |
| `identity.seed` | integer | `1` | Auto-increment start |
| `identity.increment` | integer | `1` | Auto-increment step |
| `computed.expression` | string | `null` | Computed column SQL |
| `computed.persisted` | boolean | `false` | Store computed value |
| `checkConstraint` | string | `null` | Column-level CHECK |
| `checkConstraintName` | string | `null` | Stable name for column-level CHECK |
| `collation` | string | `null` | String collation |
| `comment` | string | `null` | Documentation |

---

## 5. Type System

### 5.1 Portable Types

The type system uses **discriminated unions** where each type record carries exactly the metadata it needs. Types without parameters (like `BigIntType`) have none. Types with parameters (like `DecimalType(int Precision, int Scale)`) carry only what they need.

Pattern match on type to generate platform-specific DDL:

```csharp
public static string ToSqlServerType(PortableType type) => type switch
{
    BigIntType => "BIGINT",
    DecimalType(var p, var s) => $"DECIMAL({p},{s})",
    VarCharType(var max) => $"VARCHAR({max})",
    TextType => "NVARCHAR(MAX)",
    UuidType => "UNIQUEIDENTIFIER",
    // ... etc
};
```

### 5.2 Type Mapping Table

Complete mapping of all discriminated union types to platform-specific DDL:

#### Integer Types

| Portable Type | SQLite | PostgreSQL | SQL Server |
|---------------|--------|------------|------------|
| `TinyIntType` | INTEGER | SMALLINT | TINYINT |
| `SmallIntType` | INTEGER | SMALLINT | SMALLINT |
| `IntType` | INTEGER | INTEGER | INT |
| `BigIntType` | INTEGER | BIGINT | BIGINT |

#### Exact Numeric Types

| Portable Type | SQLite | PostgreSQL | SQL Server |
|---------------|--------|------------|------------|
| `DecimalType(p,s)` | REAL | NUMERIC(p,s) | DECIMAL(p,s) |
| `MoneyType` | REAL | NUMERIC(19,4) | MONEY |
| `SmallMoneyType` | REAL | NUMERIC(10,4) | SMALLMONEY |

#### Floating Point Types

| Portable Type | SQLite | PostgreSQL | SQL Server |
|---------------|--------|------------|------------|
| `FloatType` | REAL | REAL | REAL |
| `DoubleType` | REAL | DOUBLE PRECISION | FLOAT |

#### String Types

| Portable Type | SQLite | PostgreSQL | SQL Server | Notes |
|---------------|--------|------------|------------|-------|
| `CharType(n)` | TEXT | CHAR(n) | CHAR(n) | Fixed-length, padded |
| `VarCharType(n)` | TEXT | VARCHAR(n) | VARCHAR(n) | Variable, single-byte |
| `NCharType(n)` | TEXT | CHAR(n) | NCHAR(n) | Fixed-length, Unicode |
| `NVarCharType(n)` | TEXT | VARCHAR(n) | NVARCHAR(n) | Variable, Unicode |
| `NVarCharType(MAX)` | TEXT | TEXT | NVARCHAR(MAX) | n = int.MaxValue |
| `TextType` | TEXT | TEXT | NVARCHAR(MAX) | Unlimited |

#### Binary Types

| Portable Type | SQLite | PostgreSQL | SQL Server | Notes |
|---------------|--------|------------|------------|-------|
| `BinaryType(n)` | BLOB | BYTEA | BINARY(n) | Fixed-length |
| `VarBinaryType(n)` | BLOB | BYTEA | VARBINARY(n) | Variable |
| `VarBinaryType(MAX)` | BLOB | BYTEA | VARBINARY(MAX) | n = int.MaxValue |
| `BlobType` | BLOB | BYTEA | VARBINARY(MAX) | Unlimited |

#### Date/Time Types

| Portable Type | SQLite | PostgreSQL | SQL Server | Notes |
|---------------|--------|------------|------------|-------|
| `DateType` | TEXT | DATE | DATE | Date only |
| `TimeType(p)` | TEXT | TIME(p) | TIME(p) | p = 0-7 precision |
| `DateTimeType(p)` | TEXT | TIMESTAMP | DATETIME2(p) | p = 0-7 precision |
| `DateTimeOffsetType` | TEXT | TIMESTAMPTZ | DATETIMEOFFSET | With timezone |
| `RowVersionType` | BLOB | BYTEA | ROWVERSION | Concurrency token |

#### Other Types

| Portable Type | SQLite | PostgreSQL | SQL Server | Notes |
|---------------|--------|------------|------------|-------|
| `UuidType` | TEXT | UUID | UNIQUEIDENTIFIER | 128-bit GUID |
| `BooleanType` | INTEGER | BOOLEAN | BIT | True/false |
| `JsonType` | TEXT | JSONB | NVARCHAR(MAX) | JSON document |
| `XmlType` | TEXT | XML | XML | XML document |
| `EnumType(name, vals)` | TEXT | {name} | NVARCHAR(100) | + CHECK constraint |
| `GeometryType(srid)` | BLOB | GEOMETRY | GEOMETRY | Spatial data |
| `GeographyType(srid)` | BLOB | GEOGRAPHY | GEOGRAPHY | Earth-surface GIS |
| `VectorType(dims)` | vec0 virtual table (sqlite-vec) | vector(dims) (pgvector) | VECTOR(dims) (native, MSSQL 2025+) | Dense float embedding — see [MIG-TYPES-VECTOR]. **REQUIRED on every backend.** |

#### SQLite Type Affinity Notes

SQLite uses type affinity rather than strict types. The migration framework stores the full portable type in metadata to preserve precision/length information even though SQLite only has 5 storage classes:

| SQLite Affinity | Storage | Portable Types Mapped |
|-----------------|---------|----------------------|
| INTEGER | 64-bit signed | All int types, boolean |
| REAL | 64-bit float | Float, double, decimal |
| TEXT | UTF-8/16 string | All string types, datetime, uuid, json, xml, enum |
| BLOB | Raw bytes | All binary types, geometry, geography |
| NULL | Null value | (any nullable column) |

To preserve type metadata for upgrades, store the original portable type definition in a `__schema_metadata` table.

### 5.3 Identity/Auto-Increment

Identity columns are handled per-platform:

| Platform | Identity Syntax |
|----------|----------------|
| SQLite | `INTEGER PRIMARY KEY` (implicit ROWID alias) |
| PostgreSQL | `SERIAL` / `BIGSERIAL` or `GENERATED ALWAYS AS IDENTITY` |
| SQL Server | `IDENTITY(1,1)` |

### 5.4 Vector / Embedding Columns [MIG-TYPES-VECTOR]

> **NORMATIVE / RIGID.** Vector support is a **first-class, cross-backend** feature. It MUST work identically — storage, retrieval, similarity search, index acceleration — on **every** supported backend: PostgreSQL, SQLite, SQL Server. There is no "fallback to opaque bytes" tier. A backend that cannot host vectors is not a supported backend.

Dense float embedding columns (e.g. OpenAI `text-embedding-3-small` at 1536 dims, BGE-small at 384 dims, MedEmbed-Small at 384 dims, BERT at 768 dims) are modelled as `VectorType(int Dimensions)`.

Per-backend implementation:

| Backend | Storage | Extension/Version | Similarity |
|---|---|---|---|
| PostgreSQL | `vector(N)` column | [pgvector](https://github.com/pgvector/pgvector) extension (any supported PG version) | `<->` (L2), `<#>` (inner product), `<=>` (cosine) |
| SQLite | `vec0` virtual table backing a `FLOAT[N]` column | [sqlite-vec](https://github.com/asg017/sqlite-vec) extension (loaded at connection open) | `vec_distance_L2`, `vec_distance_cosine`, `vec_distance_dot` scalar functions |
| SQL Server | `VECTOR(N)` column | Native type, **requires SQL Server 2025 or Azure SQL Database** | `VECTOR_DISTANCE('cosine' \| 'euclidean' \| 'dot', col, @q)` scalar function |

#### 5.4.1 YAML Syntax

Vector columns use the **inline-parenthetical** convention to match `Decimal(p,s)`, `VarChar(n)`, `Geometry(srid)`:

```yaml
tables:
  - name: Document
    columns:
      - name: Id
        type: Uuid
        nullable: false
      - name: Embedding
        type: Vector(384)
        nullable: true
```

`Vector(N)` is **REQUIRED** to specify `N` as a positive integer literal. A bare `Vector` with no dimension is a parse error (`MIG-E-VECTOR-DIMS-MISSING`). Dimensions must be `1..16000` (pgvector hard limit); out of range is `MIG-E-VECTOR-DIMS-RANGE`.

#### 5.4.2 PostgreSQL DDL

For a Postgres target with **any** `VectorType` column anywhere in the schema, `PostgresDdlGenerator` prepends the extension statement to its output once per migration batch:

```sql
CREATE EXTENSION IF NOT EXISTS vector;
```

Column emission uses the native pgvector type:

```sql
"Embedding" vector(384)
```

No data cast, no `text` fallback. The extension prologue runs in the same transaction as the rest of the migration; if the Postgres role lacks `CREATE EXTENSION` permission the migration fails with `MIG-E-VECTOR-EXT-PERM` and no partial state is committed.

#### 5.4.3 Vector Indexes [MIG-TYPES-VECTOR-INDEX]

pgvector ships two index types: **IVFFlat** (fast build, approximate) and **HNSW** (slower build, better recall). Schema YAML expresses them via an extended `indexes:` entry:

```yaml
indexes:
  - name: IX_Document_Embedding_Cosine
    columns: [Embedding]
    index_type: ivfflat        # ivfflat | hnsw
    vector_ops: cosine         # cosine | l2 | ip
    options:
      lists: 100               # ivfflat-only
  - name: IX_Document_Embedding_HNSW
    columns: [Embedding]
    index_type: hnsw
    vector_ops: l2
    options:
      m: 16                    # hnsw-only
      ef_construction: 64      # hnsw-only
```

Mapping to pgvector DDL:

| YAML `vector_ops` | pgvector operator class |
|---|---|
| `cosine` | `vector_cosine_ops` |
| `l2` | `vector_l2_ops` |
| `ip` | `vector_ip_ops` |

Emitted DDL (example):

```sql
CREATE INDEX "IX_Document_Embedding_Cosine"
  ON "public"."Document"
  USING ivfflat ("Embedding" vector_cosine_ops)
  WITH (lists = 100);
```

Index-type validation rules:

- `index_type: ivfflat` requires `options.lists` (positive int, default `100` if omitted).
- `index_type: hnsw` accepts optional `options.m` (default `16`) and `options.ef_construction` (default `64`).
- Specifying `lists` under `hnsw` or `m`/`ef_construction` under `ivfflat` is a parse error (`MIG-E-VECTOR-IDX-OPTIONS`).
- Using `index_type: ivfflat` / `hnsw` on a non-`VectorType` column is a parse error (`MIG-E-VECTOR-IDX-NONVECTOR`).

#### 5.4.4 Schema Inspection

`PostgresSchemaInspector` reverse-maps a pgvector column to `VectorType(N)` by reading `pg_attribute.atttypmod` for the `vector` type (the dimension is encoded directly in `atttypmod`). `information_schema.columns.data_type` reports `USER-DEFINED` for extension types, so the inspector must join `pg_type` / `pg_attribute` to resolve the backing type name and its modifier. An unrecognised `atttypmod` for `vector` is treated as `MIG-E-VECTOR-INTROSPECT` and the table is skipped with a logged warning, not a hard failure.

#### 5.4.5 Codegen C# Type [DP-CODEGEN-VECTOR]

DataProvider codegen emits vector columns as **`float[]`** in record types, insert/update binders, and select readers on **every backend**. The same C# shape compiles and runs against PostgreSQL, SQLite (via sqlite-vec), and SQL Server (native `VECTOR`). `ReadOnlyMemory<float>` is only emitted when requested via an explicit `--vector-repr=readonly-memory` flag (future; not in 0.9.0-beta).

Per-backend binder:

| Backend | Reader | Writer | Required runtime package |
|---|---|---|---|
| PostgreSQL | `reader.GetFieldValue<Pgvector.Vector>(i).ToArray()` | `new NpgsqlParameter { Value = new Pgvector.Vector(arr) }` via `NpgsqlDataSourceBuilder.UseVector()` | `Pgvector.Npgsql` |
| SQLite | `reader.GetFieldValue<byte[]>(i)` → `MemoryMarshal.Cast<byte,float>(...).ToArray()` (sqlite-vec stores as little-endian `float32[]` blob within the virtual table; the codegen hides the marshalling) | `cmd.Parameters.AddWithValue("@e", MemoryMarshal.AsBytes(arr.AsSpan()).ToArray())` targeting `T__vec_{col}` | `Microsoft.Data.Sqlite` (already) + sqlite-vec native binaries shipped by DataProviderMigrate |
| SQL Server | `reader.GetFieldValue<float[]>(i)` (native via `SqlDbType.Vector`) | `new SqlParameter { SqlDbType = SqlDbType.Vector, Value = arr }` | `Microsoft.Data.SqlClient` (version supporting `SqlDbType.Vector`) |

**The DataProvider codegen tool's own dependencies** grow to include `Pgvector.Npgsql` (so Postgres schema introspection can resolve the vector type) and the RID-specific sqlite-vec native binaries (so SQLite introspection can open a connection with the extension loaded). SQL Server introspection needs no additional dependency beyond `Microsoft.Data.SqlClient` at a version that reports `VECTOR` in `sys.types`.

**Consumer runtime csproj** must add the matching package:

- Postgres consumers: `<PackageReference Include="Pgvector.Npgsql" Version="*" />`
- SQLite consumers: nothing extra — sqlite-vec loads via the native binaries shipped by the DataProvider generated code at connection open. The generated code resolves the RID-appropriate binary from its own `runtimes/{rid}/native/` subtree.
- SQL Server consumers: `Microsoft.Data.SqlClient` at a GA version that exposes `SqlDbType.Vector`.

Vector columns are **not** nullable in the default case. If the YAML marks `nullable: true`, the generated C# type is `float[]?` and every binder handles `DBNull` / `NULL` correctly. Empty vectors (`new float[0]`) are rejected at bind time with `DPSG-VEC-EMPTY`; a null intent must use `null`, not an empty array.

#### 5.4.6 SQLite via sqlite-vec [MIG-TYPES-VECTOR-SQLITE]

SQLite has no native vector type. DataProviderMigrate integrates [sqlite-vec](https://github.com/asg017/sqlite-vec) (MIT, actively maintained, cross-platform) to deliver first-class vector storage and similarity search on SQLite, matching the Postgres contract.

**Extension loading.** `SqliteDdlGenerator` and `SqliteSchemaInspector` both require the sqlite-vec extension loaded on the `SqliteConnection` before any DDL or introspection runs. DataProviderMigrate ships the native binaries (`vec0.dll` / `libsqlite_vec.so` / `libsqlite_vec.dylib`) for `win-x64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64` inside the `DataProviderMigrate` tool package under `runtimes/{rid}/native/`. The migration runner opens the connection with `EnableExtensions = true`, locates the RID-appropriate binary, and calls `connection.LoadExtension("vec0")`. Failure to load is `MIG-E-VECTOR-SQLITE-LOAD`.

**Storage schema.** For a portable table `T` with one or more `VectorType` columns, the SQLite DDL splits into two objects:

1. A regular SQLite `CREATE TABLE "T" (...)` for every **non-vector** column, with the table's primary key unchanged.
2. One `CREATE VIRTUAL TABLE "T__vec_{col}" USING vec0(rowid INTEGER PRIMARY KEY, embedding FLOAT[{N}])` per vector column. Rows are keyed by the base table's integer rowid for 1:1 correspondence.

The migration runner maintains referential integrity through triggers generated alongside the virtual table:

- `INSERT ON "T"` → insert a zero-filled placeholder into `T__vec_{col}` at the new rowid (actual vector value flows through the generated INSERT path, which targets the virtual table directly — see [DP-CODEGEN-VECTOR-SQLITE]).
- `DELETE ON "T"` → delete the matching rowid from `T__vec_{col}`.
- `UPDATE` of the vector column → `UPDATE "T__vec_{col}" SET embedding = ? WHERE rowid = ?`.

This layout is invisible to consumers: the codegen presents the vector column as if it were a first-class column on `T`.

**Similarity search.** sqlite-vec exposes scalar functions that DataProvider's Postgres LQL transpile and codegen paths alias to the same LQL surface:

| LQL builtin | pgvector operator | sqlite-vec function | MSSQL function |
|---|---|---|---|
| `cosine_distance(col, @q)` | `col <=> @q` | `vec_distance_cosine(embedding, @q)` | `VECTOR_DISTANCE('cosine', col, @q)` |
| `l2_distance(col, @q)` | `col <-> @q` | `vec_distance_L2(embedding, @q)` | `VECTOR_DISTANCE('euclidean', col, @q)` |
| `inner_product(col, @q)` | `col <#> @q` | `vec_distance_dot(embedding, @q)` | `VECTOR_DISTANCE('dot', col, @q)` |

**Indexes.** `vec0` is itself an index. `index_type: ivfflat` and `index_type: hnsw` are **accepted but silently mapped to the default `vec0` ANN** on SQLite (vec0 uses brute-force by default and is evolving ANN support). `MIG-E-VECTOR-IDX-OPTIONS` still applies for mismatched options; the options are simply ignored on the SQLite backend. A future sqlite-vec release may honour `ivfflat.lists` / `hnsw.m` directly; until then consumers get correct results at lower speed, never wrong results.

**Dimension round-trip.** `vec0`'s declared schema is `FLOAT[N]` — dimension is preserved in the virtual table's schema and read back by `SqliteSchemaInspector` via `PRAGMA table_xinfo("T__vec_{col}")` + parsing the `FLOAT[N]` decl. Fallback: `__schema_metadata` carries the canonical `VectorType(N)` when sqlite-vec changes its metadata shape.

#### 5.4.7 SQL Server via native VECTOR [MIG-TYPES-VECTOR-MSSQL]

SQL Server 2025 and Azure SQL Database ship a first-class `VECTOR(N)` column type and the `VECTOR_DISTANCE` scalar function. DataProviderMigrate targets this natively.

**DDL.** `SqlServerDdlGenerator` maps `VectorType(N)` to:

```sql
[Embedding] VECTOR(384) NULL
```

No extension, no prelude. The runner probes `SELECT SERVERPROPERTY('ProductMajorVersion')` at migration start and fails with `MIG-E-VECTOR-MSSQL-VERSION` if the server is older than SQL Server 2025 / unsupported Azure SQL edition. There is no downgrade path — if you need vectors on older SQL Server you must upgrade the server. This matches the user directive: "MUST work on any database" means any supported database; unsupported versions of SQL Server are not supported for vector schemas, identically to how unsupported Postgres versions are not supported for `JSONB`.

**Similarity search.** Emitted at codegen time:

```sql
SELECT TOP (@k) *
FROM [Document]
ORDER BY VECTOR_DISTANCE('cosine', [Embedding], @q);
```

**Indexes.** SQL Server 2025's native `VECTOR` ships without ANN index support at GA (columnstore + brute force). `index_type: ivfflat` / `hnsw` on SQL Server targets emit a warning `MIG-W-VECTOR-MSSQL-ANN` and create a standard B-Tree index on the vector column (useless for ANN but not wrong). This caveat is documented for consumers; it will lift when SQL Server adds ANN index types.

**Parameter binding.** `Microsoft.Data.SqlClient` accepts `float[]` via `SqlDbType.Vector` (shipped alongside the GA `VECTOR(n)` type). DataProvider codegen emits the same `float[]` C# shape on all three backends — the per-backend binder handles the wire format.

#### 5.4.8 Diagnostics

| Code | Severity | Meaning |
|---|---|---|
| `MIG-E-VECTOR-DIMS-MISSING` | Error | `type: Vector` without `(N)` dimension |
| `MIG-E-VECTOR-DIMS-RANGE` | Error | Dimension not in `1..16000` |
| `MIG-E-VECTOR-IDX-OPTIONS` | Error | Option mismatch between `ivfflat` / `hnsw` (e.g. `lists` on hnsw) |
| `MIG-E-VECTOR-IDX-NONVECTOR` | Error | `index_type: ivfflat`/`hnsw` on a non-vector column |
| `MIG-E-VECTOR-PG-EXT-PERM` | Error | `CREATE EXTENSION vector` failed — missing permission or pgvector not installed on the Postgres host |
| `MIG-E-VECTOR-PG-INTROSPECT` | Warn | `pg_attribute.atttypmod` could not be decoded for a `vector` column; table skipped |
| `MIG-E-VECTOR-SQLITE-LOAD` | Error | `sqlite-vec` native binary could not be loaded (missing RID-specific asset, or `EnableExtensions=false` on the connection) |
| `MIG-E-VECTOR-SQLITE-VEC0` | Error | `CREATE VIRTUAL TABLE ... USING vec0(...)` failed — sqlite-vec reports an invalid shape or dimension |
| `MIG-E-VECTOR-MSSQL-VERSION` | Error | Target SQL Server is older than SQL Server 2025 / unsupported Azure SQL edition; native `VECTOR(N)` unavailable |
| `MIG-W-VECTOR-MSSQL-ANN` | Warn | `index_type: ivfflat` / `hnsw` requested on SQL Server; emitted as B-Tree (no ANN in SQL Server 2025 GA) |
| `DPSG-VEC-EMPTY` | Error | Writer bound an empty `float[]` to a vector parameter; use `null` for absent, never an empty array |

---

## 6. Schema Operations

### 6.1 Operation Types

The diff engine produces a list of schema operations as discriminated union records:

- **Table**: `CreateTable`, `DropTable`
- **Column**: `AddColumn`, `DropColumn`, `AlterColumn`
- **Index**: `CreateIndex`, `DropIndex`
- **Constraint**: `AddPrimaryKey`, `DropPrimaryKey`, `AddForeignKey`, `DropForeignKey`, `AddUniqueConstraint`, `AddCheckConstraint`

All operations carry the schema name, table name, and relevant definition or constraint name.

### 6.2 Additive-Only Mode (Default)

By default, the migration engine only applies **additive** operations:

| Operation | Allowed by Default |
|-----------|-------------------|
| `CreateTable` | Yes |
| `AddColumn` | Yes |
| `CreateIndex` | Yes |
| `AddPrimaryKey` | Yes |
| `AddForeignKey` | Yes |
| `DropTable` | **No** - requires explicit opt-in |
| `DropColumn` | **No** - requires explicit opt-in |
| `DropIndex` | **No** - requires explicit opt-in |
| `AlterColumn` | **No** - requires explicit opt-in |

### 6.3 Destructive Operations

Destructive operations require explicit opt-in via `MigrationOptions`:

- `AllowDropTable` (default: false)
- `AllowDropColumn` (default: false)
- `AllowDropIndex` (default: false)
- `AllowAlterColumn` (default: false)

### PostgreSQL Constraint-Backed Index Drops [MIG-PG-CONSTRAINT-BACKED-INDEX-DROP]

When a destructive diff emits `DropIndex` for a PostgreSQL index that implements
a `UNIQUE` or `PRIMARY KEY` constraint, the provider must drop the owning
constraint with `ALTER TABLE ... DROP CONSTRAINT` instead of issuing `DROP INDEX`.
Detection uses `pg_constraint.conindid` joined to the target table and index.
Indexes that are not owned by a constraint still use `DROP INDEX IF EXISTS`.

### PostgreSQL Unique Constraint Inspection [MIG-PG-UNIQUE-CONSTRAINT-INSPECTION]

PostgreSQL schema inspection must report `UNIQUE` constraints from
`pg_constraint` as `UniqueConstraints`, preserving the constraint name and
column order. Indexes owned by `UNIQUE` or `PRIMARY KEY` constraints are not
ordinary `Indexes` in the portable schema model. A destructive diff against a
converged schema must not emit `DropIndex` for a backing index when the desired
schema still declares the owning unique constraint.

### PostgreSQL Named Column Check Constraints [MIG-PG-NAMED-COLUMN-CHECK-CONSTRAINT]

Column-level `checkConstraint` entries may specify `checkConstraintName`.
PostgreSQL DDL must emit `CONSTRAINT "<name>" CHECK (...)` for that column
constraint so migrations create stable, queryable `pg_constraint` rows. When no
name is supplied, the provider uses `<table>_<column>_chk`. PostgreSQL schema
inspection must preserve the discovered constraint name for one-column checks so
idempotency proofs can verify the constraint was materialized.

---

## 7. Migration Execution

### 7.1 Migration Runner

`MigrationRunner` executes operations with transaction handling. Key methods:

- `Apply(connection, operations, options, logger)` → `MigrationResult`
- `GenerateDdl(operations, platform)` → `Result<string, MigrationError>` (preview without executing)

### 7.2 Transaction Strategy

| Platform | Transaction Behavior |
|----------|---------------------|
| SQLite | Single transaction for all DDL (SQLite supports transactional DDL) |
| PostgreSQL | Single transaction for all DDL (PostgreSQL supports transactional DDL) |
| SQL Server | Per-statement (SQL Server DDL has transaction limitations) |

### 7.3 Execution Flow

```
1. Validate all operations against options (fail fast for disallowed destructive ops)
2. Begin transaction (if supported)
3. For each operation:
   a. Generate platform-specific DDL
   b. Log operation details
   c. Execute DDL
   d. Verify success
4. Commit transaction (or rollback on error)
5. Return result with applied operations
```

---

## 8. Diff Engine

### 8.1 Schema Comparison

`SchemaDiff.Calculate(current, desired)` compares desired schema against current database state and returns the list of operations needed to transform current into desired.

### 8.2 Comparison Rules

| Element | Comparison Logic |
|---------|-----------------|
| Tables | Match by schema + name (case-insensitive) |
| Columns | Match by name within table (case-insensitive) |
| Indexes | Match by name (case-insensitive) |
| Primary Keys | Match by table (only one per table) |
| Foreign Keys | Match by name (case-insensitive) |

### 8.3 Diff Algorithm

```
For each table in desired schema:
    If table not in current:
        Emit CreateTable
    Else:
        For each column in desired table:
            If column not in current table:
                Emit AddColumn
            Else if column differs:
                Emit AlterColumn

        For each column in current table not in desired:
            Emit DropColumn

        Compare indexes, primary key, foreign keys similarly

For each table in current not in desired:
    Emit DropTable
```

### 8.4 Schema Introspection

Each provider implements `SchemaInspector.Inspect(connection, logger)` → `Result<SchemaDefinition, MigrationError>` to read the current database schema.

### 8.5 Schema Capture to Metadata

**CRITICAL REQUIREMENT**: The migration framework MUST support capturing an existing database schema and persisting it to metadata. This enables:

1. **Brownfield Adoption**: Capture schema from existing production databases
2. **Schema Versioning**: Store captured schema as JSON for version control
3. **Cross-Platform Migration**: Capture from one platform, apply to another
4. **Audit Trail**: Record point-in-time schema snapshots

#### 8.5.1 Schema Capture Workflow

1. Connect to existing database
2. Call `SchemaInspector.Inspect()` to capture current schema
3. Call `SchemaSerializer.ToYaml()` to serialize for version control
4. Later: `SchemaSerializer.FromYaml()` to load, then `SchemaDiff.Calculate()` and `MigrationRunner.Apply()`

#### 8.5.2 Captured Schema Contents

The schema inspector MUST capture:

| Element | Required |
|---------|----------|
| All tables in schema | Yes |
| All columns with types | Yes |
| Primary keys | Yes |
| Indexes (non-primary) | Yes |
| Foreign keys with actions | Yes |
| Default values | Yes |
| NOT NULL constraints | Yes |
| Identity/auto-increment | Yes |

---

## 9. Database Providers

### 9.1 Provider Interface

`DdlGenerator.Generate(operation, platform)` produces platform-specific DDL SQL.

Platforms: `SQLite`, `PostgreSQL`, `SqlServer`

### 9.2 SQLite Provider

SQLite-specific considerations:

- No native UUID type (uses TEXT)
- No native BOOLEAN type (uses INTEGER 0/1)
- No ALTER COLUMN support (requires table rebuild)
- No DROP COLUMN before SQLite 3.35 (requires table rebuild)
- Transactional DDL supported

### 9.3 PostgreSQL Provider

PostgreSQL-specific considerations:

- Native UUID, BOOLEAN, JSONB types
- Full ALTER COLUMN support
- Partial index support
- Transactional DDL supported
- Case-sensitive identifiers (lowercase by default)

### 9.4 SQL Server Provider

SQL Server-specific considerations:

- NVARCHAR for Unicode strings
- UNIQUEIDENTIFIER for UUIDs
- Limited transactional DDL
- Schema support (dbo, etc.)

---

## 10. Error Handling

### 10.1 Error Types

All errors extend `MigrationError(Message)`:

- **IntrospectionError** - Failed to read database schema
- **DdlGenerationError** - Failed to generate DDL for an operation
- **ExecutionError** - DDL execution failed (includes SQL that failed)
- **ValidationError** - Operation not allowed (e.g., destructive op without opt-in)

### 10.2 Result Types

All operations return Result types (never throw). Common aliases:

- `MigrationResult = Result<MigrationSummary, MigrationError>`
- `InspectionResult = Result<SchemaDefinition, MigrationError>`
- `DdlResult = Result<string, MigrationError>`

---

## 11. Conformance Requirements

An implementation is **conformant** if:

1. Schema definitions are database-agnostic records
2. All portable types map correctly to each supported platform
3. Diff engine correctly identifies additive operations
4. Destructive operations require explicit opt-in
5. DDL generation produces valid SQL for each platform
6. Migration runner handles transactions appropriately per platform
7. Schema introspection correctly reads existing schema
8. All operations return Result types (never throw for expected errors)
9. All public members have XML documentation
10. Logging via ILogger at appropriate levels
11. E2E tests cover greenfield creation and upgrade scenarios
12. E2E tests run against real databases (SQLite in-memory, PostgreSQL via Testcontainers)

---

## 12. E2E Testing Requirements

End-to-end tests are **critical** for validating that migrations work correctly against real databases. No mocks allowed.

### 12.1 Test Categories

| Category | Description |
|----------|-------------|
| **Greenfield** | Create database from scratch using schema definition |
| **Upgrade** | Add tables/columns/indexes to existing database |
| **Idempotency** | Run same migration twice, verify no errors |
| **Cross-Platform** | Same schema definition works on SQLite, PostgreSQL, SQL Server |
| **Introspection** | Verify inspected schema matches created schema |

### 12.2 Greenfield Tests

Create fresh database, define schema with fluent API, apply via `SchemaDiff.Calculate()` + `MigrationRunner.Apply()`, verify tables exist via introspection.

### 12.3 Upgrade Tests

Apply v1 schema, then v2 with new columns. Verify diff produces `AddColumn` operations and final schema has all columns.

### 12.4 PostgreSQL Tests with Testcontainers

Use `Testcontainers.PostgreSql` to spin up real PostgreSQL. Verify native types (UUID, JSONB, TIMESTAMPTZ) are created correctly by querying `information_schema.columns`.

### 12.5 Idempotency Tests

Run migration twice. Second run should produce zero operations (schema already matches desired state).

### 12.6 Cross-Platform Test Matrix

Use `[Theory]` with `[MemberData]` to run the same schema definition against SQLite, PostgreSQL, and SQL Server. Verify identical results across all platforms.

### 12.7 Required Test Coverage

An implementation MUST include tests for:

| Scenario | SQLite | PostgreSQL | SQL Server |
|----------|--------|------------|------------|
| Create single table | Required | Required | Required |
| Create table with all portable types | Required | Required | Required |
| Create table with indexes | Required | Required | Required |
| Create table with foreign keys | Required | Required | Required |
| Add column to existing table | Required | Required | Required |
| Add index to existing table | Required | Required | Required |
| Add foreign key to existing table | Required | Required | Required |
| Idempotent migration | Required | Required | Required |
| Introspect and round-trip schema | Required | Required | Required |
| **Schema capture from existing DB** | Required | Required | Required |
| **Schema serialize to YAML metadata** | Required | Required | Required |
| **Destructive op returns useful error** | Required | Required | Required |

---

## 13. Schema Capture and Metadata

A critical feature of the Migration framework is the ability to **capture existing database schemas** and serialize them to YAML. This enables:

1. **Brownfield scenarios** - Capture existing database schema before applying migrations
2. **Schema versioning** - Store schema snapshots in source control
3. **Documentation** - Generate schema documentation from metadata
4. **Validation** - Compare captured schema against expected schema
5. **CI/CD** - Verify schema matches expected state in deployment pipelines

### 13.1 Schema Serializer

`SchemaSerializer.ToYaml(schema)` and `SchemaSerializer.FromYaml(yaml)` enable round-trip serialization for version control and brownfield adoption.

### 13.2 Required Schema Capture Tests

1. **Capture existing database** - Create DB with raw SQL, call inspector, verify complete schema returned
2. **YAML round-trip** - Serialize schema to YAML, deserialize, verify equality

---

## 14. Appendices

### Appendix A: Sync Framework Schema

The Sync framework uses Migration to create infrastructure tables: `_sync_state`, `_sync_session`, `_sync_log`, `_sync_clients`, `_sync_subscriptions`. See Sync framework documentation for details.

### Appendix B: Example Usage

Typical workflow:

1. Define schema with fluent `Schema.Define()` API
2. Open connection, call `SchemaInspector.Inspect()` to get current state
3. Call `SchemaDiff.Calculate(current, desired)` to get operations
4. Call `MigrationRunner.Apply()` with operations and options
5. Log applied operation count from result

### Appendix C: Platform-Specific DDL Examples

**Create Table - SQLite:**
```sql
CREATE TABLE IF NOT EXISTS Users (
    Id TEXT PRIMARY KEY,
    Email TEXT NOT NULL,
    Name TEXT,
    CreatedAt TEXT NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS idx_users_email ON Users(Email);
```

**Create Table - PostgreSQL:**
```sql
CREATE TABLE IF NOT EXISTS users (
    id UUID PRIMARY KEY,
    email VARCHAR(255) NOT NULL,
    name VARCHAR(100),
    created_at TIMESTAMP NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS idx_users_email ON users(email);
```

**Create Table - SQL Server:**
```sql
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
CREATE TABLE Users (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    Email NVARCHAR(255) NOT NULL,
    Name NVARCHAR(100),
    CreatedAt DATETIME2 NOT NULL
);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_users_email')
CREATE UNIQUE INDEX idx_users_email ON Users(Email);
```

---

## References

- [SQLite CREATE TABLE](https://sqlite.org/lang_createtable.html)
- [PostgreSQL CREATE TABLE](https://www.postgresql.org/docs/current/sql-createtable.html)
- [SQL Server CREATE TABLE](https://docs.microsoft.com/en-us/sql/t-sql/statements/create-table-transact-sql)
- [Prisma Migrate](https://www.prisma.io/docs/orm/prisma-migrate)
