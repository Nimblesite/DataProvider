# .NET Sync Framework Specification

## Table of Contents

1. [Introduction](#1-introduction)
2. [Goals & Non-Goals](#2-goals--non-goals)
3. [Architecture Overview](#3-architecture-overview)
4. [Primary Key Requirements](#4-primary-key-requirements)
5. [Origin Identification](#5-origin-identification)
6. [Timestamps](#6-timestamps)
7. [Data Mapping](#7-data-mapping)
8. [Unified Change Log](#8-unified-change-log)
9. [Trigger Suppression](#9-trigger-suppression)
10. [LQL Trigger Generation](#10-lql-trigger-generation)
11. [Real-Time Subscriptions](#11-real-time-subscriptions)
12. [Bi-Directional Sync Protocol](#12-bi-directional-sync-protocol)
13. [Batching](#13-batching)
14. [Tombstone Retention](#14-tombstone-retention)
15. [Conflict Resolution](#15-conflict-resolution)
16. [Hash Verification](#16-hash-verification)
17. [Security Considerations](#17-security-considerations)
18. [Schema Metadata](#18-schema-metadata)
19. [Conformance Requirements](#19-conformance-requirements)
20. [Appendices](#20-appendices)
21. [References](#21-references)

---

## 1. Introduction

This specification defines a two-way synchronization framework for .NET applications. The framework enables offline-first workloads, multi-device synchronization, and eventual consistency across distributed replicas.

### 1.1 Scope

This specification covers:

- Row-level change capture via triggers
- Unified change log with JSON payloads
- **Data mapping between heterogeneous schemas** (source→target table/column transforms)
- Version-based change enumeration with batching
- Tombstone management for deletions
- Hash-based database verification
- Bi-directional sync ordering
- Trigger suppression during sync
- Sync state tracking (which records have been synced)

This specification does **not** cover:

- Network transport protocols
- Authentication/authorization
- Specific conflict resolution algorithms (pluggable)
- Complex ETL transformations (mapping is row-level, not aggregation)

### 1.2 Target Platforms

- SQLite (primary - requires trigger-based tracking)
- SQL Server (can leverage native Change Tracking)
- PostgreSQL (can leverage logical replication)

#### [SYNC-HTTP-PROVIDERS] Current HTTP implementation

The shipped replica providers and HTTP sync operations support SQLite and PostgreSQL.
SQL Server remains a target platform; no SQL Server sync replica provider is implemented.
HTTP requests for that provider currently return `ProblemDetails` with status 500 and
`Unknown database type: sqlserver`, before opening or modifying the target database.
Provider regression tests must verify this rejection and an unchanged catalog. The
SQLite and PostgreSQL cases must continue verifying real reads and applied changes.

---

## 2. Goals & Non-Goals

### 2.1 Goals

- **Minimal footprint**: Lightweight tracking with low storage overhead
- **Database-agnostic design**: Core concepts portable across engines
- **Offline-first**: Full functionality without network connectivity
- **Deterministic sync**: Reproducible change ordering via monotonic versions
- **Extensible conflict handling**: Pluggable resolution strategies
- **Hash-based verification**: Every sync produces a verifiable hash
- **Efficient batching**: Handle millions of records for long-offline scenarios
- **Heterogeneous schema sync**: Map source tables/columns to different target structures (microservices, data lakes)
- **Selective sync**: Choose which tables, columns, and records to sync per direction

### 2.2 Non-Goals

- CRDT-based automatic merge (out of scope, but extensible)
- Binary blob or file system synchronization
- Complex ETL (aggregations, joins across tables) - this is row-level mapping only

---

## 3. Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│                    Application Layer                     │
├─────────────────────────────────────────────────────────┤
│                    Sync Engine                           │
│  ┌───────────┐ ┌───────────┐ ┌───────────┐ ┌─────────┐  │
│  │  Batch    │ │  Change   │ │  Conflict │ │  Hash   │  │
│  │  Manager  │ │Enumerator │ │  Resolver │ │ Verifier│  │
│  └───────────┘ └───────────┘ └───────────┘ └─────────┘  │
├─────────────────────────────────────────────────────────┤
│                  Tracking Layer                          │
│         ┌─────────────────────────────────┐              │
│         │   Unified Change Log (_sync)    │              │
│         │   - Trigger suppression flag    │              │
│         │   - JSON payloads               │              │
│         │   - Inserts, updates, deletes   │              │
│         └─────────────────────────────────┘              │
├─────────────────────────────────────────────────────────┤
│                    Database Layer                        │
│              (SQLite / SQL Server / PostgreSQL)          │
└─────────────────────────────────────────────────────────┘
```

---

## 4. Primary Key Requirements

### 4.1 Single-Column UUID Primary Key (REQUIRED)

Every tracked table MUST have a single-column primary key. **UUID/GUID is strongly recommended**.

**Rationale:**
- Composite keys complicate JSON serialization, conflict detection, and FK references
- Auto-increment integers cause collision issues in distributed systems
- UUIDs can be generated offline without coordination

### 4.2 Supported PK Types

| Type | Suitability | Notes |
|------|-------------|-------|
| UUID/GUID | **Recommended** | No collision risk, offline-safe |
| ULID | Excellent | Sortable, offline-safe |
| Integer (auto-inc) | Poor | Collisions across replicas |
| Composite | **Not supported** | Simplicity over flexibility |

### 4.3 Implementation

```sql
-- Recommended: UUID primary key
CREATE TABLE Person (
    Id TEXT PRIMARY KEY,  -- UUID as text
    Name TEXT NOT NULL,
    Email TEXT
);

-- The pk_value in _sync_log is always a simple JSON string
-- {"Id": "550e8400-e29b-41d4-a716-446655440000"}
```

Implementations MAY support integer PKs for simple cases but MUST document the collision risks.

---

## 5. Origin Identification

### 5.1 Purpose

Every replica needs a unique identifier to:
- Track which changes came from where
- Prevent echo (re-applying own changes)
- Detect conflicts between replicas

### 5.2 Origin ID Generation

Origin IDs MUST be generated using UUID v4 (random).

```csharp
// Generated ONCE on first sync initialization
var originId = Guid.NewGuid().ToString();
// Store in _sync_state, never regenerate
```

### 5.3 Origin ID Lifecycle

| Event | Action |
|-------|--------|
| First app install | Generate new UUID v4, store permanently |
| App reinstall | Generate new UUID v4 (treated as new device) |
| Database restore | Keep existing origin ID from backup |
| Device clone | MUST regenerate origin ID on clone |

### 5.4 Storage

```sql
CREATE TABLE _sync_state (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

-- Set once, never change
INSERT INTO _sync_state VALUES ('origin_id', '550e8400-e29b-41d4-a716-446655440000');
```

### 5.5 Requirements

- Origin ID MUST be 36 characters (standard UUID format with hyphens)
- Origin ID MUST be stored before any sync operations
- Origin ID MUST NOT change after initial generation (except clone scenario)
- Origin ID MUST be included in every change log entry

---

## 6. Timestamps

### 6.1 Format

All timestamps MUST be stored as **ISO 8601 UTC strings** with millisecond precision:

```
2025-12-18T10:30:00.000Z
```

### 6.2 Requirements

- Timestamps MUST be UTC (indicated by `Z` suffix)
- Timestamps MUST include milliseconds (3 decimal places)
- Timestamps MUST NOT use local time zones
- Timestamps are for conflict resolution hints, NOT causal ordering (use version for ordering)

### 6.3 Implementation

**SQLite:**
```sql
-- strftime for consistent formatting
strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
-- Result: 2025-12-18T10:30:00.123Z
```

**SQL Server:**
```sql
FORMAT(SYSUTCDATETIME(), 'yyyy-MM-ddTHH:mm:ss.fffZ')
```

**PostgreSQL:**
```sql
to_char(now() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')
```

---

## 7. Data Mapping

### 7.1 The Problem

In microservices and heterogeneous database scenarios, source and target schemas differ:

- One source table may map to multiple target tables (or none)
- Column names differ between systems
- Data types require transformation
- Some fields should be excluded or computed
- Records need tracking to know if they've already been synced

Simply dumping data from database A to database B is **not sync** - it's replication. True sync requires **mapping**.

### 7.2 Mapping Architecture

Data mapping is defined using two complementary approaches:

| Approach | Use Case | UI-Friendly |
|----------|----------|-------------|
| **JSON Configuration** | Simple 1:1 table/column mappings, field exclusions | ✅ Yes |
| **LQL Expressions** | Complex transformations, computed fields, conditionals | ❌ No (code) |

This dual approach enables:
- Building configuration UIs for simple mappings
- Powerful LQL expressions for complex transformations
- Storing mapping definitions alongside sync configuration

### 7.3 Mapping Configuration Schema

```json
{
    "version": "1.0",
    "mappings": [
        {
            "id": "user-to-customer",
            "source_table": "User",
            "target_table": "Customer",
            "direction": "push",
            "enabled": true,
            "pk_mapping": {
                "source_column": "Id",
                "target_column": "CustomerId"
            },
            "column_mappings": [
                {
                    "source": "FullName",
                    "target": "Name",
                    "transform": null
                },
                {
                    "source": "EmailAddress",
                    "target": "Email",
                    "transform": null
                },
                {
                    "source": null,
                    "target": "Source",
                    "transform": "constant",
                    "value": "mobile-app"
                },
                {
                    "source": "CreatedAt",
                    "target": "RegisteredDate",
                    "transform": "lql",
                    "lql": "CreatedAt |> dateFormat('yyyy-MM-dd')"
                }
            ],
            "excluded_columns": ["PasswordHash", "SecurityStamp"],
            "filter": {
                "lql": "IsActive = true AND DeletedAt IS NULL"
            },
            "sync_tracking": {
                "enabled": true,
                "tracking_column": "_synced_version",
                "strategy": "version"
            }
        },
        {
            "id": "order-split",
            "source_table": "Order",
            "target_tables": ["OrderHeader", "OrderAudit"],
            "direction": "push",
            "enabled": true,
            "multi_target": true,
            "targets": [
                {
                    "table": "OrderHeader",
                    "column_mappings": [
                        {"source": "Id", "target": "OrderId"},
                        {"source": "CustomerId", "target": "CustomerId"},
                        {"source": "Total", "target": "Amount"}
                    ]
                },
                {
                    "table": "OrderAudit",
                    "column_mappings": [
                        {"source": "Id", "target": "OrderId"},
                        {"source": "CreatedAt", "target": "EventTime"},
                        {"source": null, "target": "EventType", "value": "created"}
                    ]
                }
            ]
        }
    ]
}
```

### 7.4 LQL Mapping Expressions

For complex transformations, use LQL's `syncMap` operator:

```lql
-- Simple column rename
User
|> syncMap({
    targetTable: "Customer",
    columns: {
        "Id": "CustomerId",
        "FullName": "Name",
        "EmailAddress": "Email"
    }
})

-- With transformations
Order
|> syncMap({
    targetTable: "OrderSummary",
    columns: {
        "Id": "OrderId",
        "Total": "Amount |> round(2)",
        "CreatedAt": "OrderDate |> dateFormat('yyyy-MM-dd')",
        "_computed": "Status |> upper()"
    },
    filter: "Status != 'cancelled'"
})

-- One-to-many: split one source into multiple targets
Order
|> syncMapMulti([
    {
        targetTable: "OrderHeader",
        columns: {"Id": "OrderId", "CustomerId": "CustId", "Total": "Amount"}
    },
    {
        targetTable: "OrderLine",
        columns: {"Id": "OrderId", "LineItems": "Items |> jsonExpand()"}
    }
])

-- Conditional mapping
Product
|> syncMap({
    targetTable: "InventoryItem",
    columns: {
        "Id": "ItemId",
        "Name": "Description",
        "Price": "UnitPrice"
    },
    when: "Category = 'physical' AND Stock > 0"
})
```

### 7.5 Sync Tracking

**The client tracks its own sync position.** When pulling changes, the client tells the server which version it last synced to via the `fromVersion` parameter. The server returns all changes after that version.

#### 7.5.1 Client-Side Position Tracking

The client stores its sync position in `_sync_state`:

```sql
-- Client tracks its own position
SELECT value FROM _sync_state WHERE key = 'last_server_version';
-- Returns: "42" (last version successfully pulled from server)
```

When pulling:
```
GET /sync/changes?fromVersion=42&limit=100
```

The server returns changes with version > 42. After successfully applying the batch, the client updates its position:

```sql
UPDATE _sync_state SET value = '142' WHERE key = 'last_server_version';
```

#### 7.5.2 Why Client Tracks Position (Not Server)

- **Simplicity**: No server-side per-client state management
- **Offline-first**: Client knows exactly what it has, even offline
- **Scalability**: Server doesn't need to track millions of clients
- **Reliability**: Client is source of truth for its own state

#### 7.5.3 Sync Decision Logic

```
CLIENT PULL:
1. Read last_server_version from _sync_state
2. Request changes from server: GET /sync/changes?fromVersion={last_server_version}
3. For each change in batch:
   a. Look up mapping for source table
   b. If no mapping: skip (or apply identity mapping)
   c. Apply column mappings and transformations
   d. Apply to local table(s)
4. Update last_server_version to max version in batch
```

### 7.6 Direction Control

Mappings are **directional** - a mapping for push may differ from pull:

```json
{
    "mappings": [
        {
            "id": "user-push",
            "source_table": "User",
            "target_table": "Customer",
            "direction": "push"
        },
        {
            "id": "user-pull",
            "source_table": "Customer",
            "target_table": "User",
            "direction": "pull",
            "column_mappings": [
                {"source": "CustomerId", "target": "Id"},
                {"source": "Name", "target": "FullName"},
                {"source": "Email", "target": "EmailAddress"}
            ]
        }
    ]
}
```

### 7.7 Unmapped Tables

Tables without mappings have two behaviors:

| Mode | Behavior |
|------|----------|
| **strict** | Unmapped tables are NOT synced (explicit opt-in) |
| **passthrough** | Unmapped tables sync with identity mapping (same schema) |

Configure via:
```json
{
    "unmapped_table_behavior": "strict",
    "mappings": [...]
}
```

### 7.8 Integration with Sync Protocol

The mapping layer integrates at these points:

1. **Change Capture** (Section 8): Triggers capture raw changes with source schema
2. **Pull Phase** (Section 12):
   - Fetch changes from server
   - Apply pull mappings to transform server→local schema
   - Track sync state per mapping
3. **Push Phase** (Section 12):
   - Collect local changes
   - Apply push mappings to transform local→server schema
   - Track sync state per mapping
4. **Conflict Resolution** (Section 15): Conflicts detected on **target** pk after mapping

### 7.9 LQL Mapping Operators

The following LQL operators support mapping:

| Operator | Purpose |
|----------|---------|
| `syncMap` | Map columns from source to single target table |
| `syncMapMulti` | Map source to multiple target tables |
| `syncFilter` | Filter which records to sync |
| `syncTransform` | Apply transformations to column values |
| `syncExclude` | Exclude columns from sync |
| `syncComputed` | Add computed columns to target |

### 7.10 Requirements

- Mappings MUST be defined via JSON configuration OR LQL expressions
- JSON configuration MUST support simple 1:1 column mappings (UI-friendly)
- LQL MUST be used for complex transformations (computed fields, conditionals)
- Each mapping MUST specify direction (push, pull, or both)
- Sync tracking MUST prevent re-syncing unchanged records
- Mappings MUST be evaluated at sync time, not trigger time
- Multi-target mappings MUST be atomic (all targets or none)
- Unmapped tables MUST follow configured behavior (strict or passthrough)

---

## 8. Unified Change Log

### 8.1 Design Rationale

All changes are stored in a **single unified table** with JSON payloads:

- Simplifies schema management (one table for everything)
- Makes schema evolution trivial (JSON is schema-agnostic)
- Enables efficient batched enumeration across all tables
- Stores tombstones naturally alongside other changes

### 8.2 Schema

```sql
CREATE TABLE _sync_log (
    version     INTEGER PRIMARY KEY AUTOINCREMENT,
    table_name  TEXT NOT NULL,
    pk_value    TEXT NOT NULL,           -- JSON: {"Id": "uuid-here"}
    operation   TEXT NOT NULL CHECK (operation IN ('insert', 'update', 'delete')),
    payload     TEXT,                    -- JSON: full row data (NULL for deletes)
    origin      TEXT NOT NULL,           -- UUID of writer
    timestamp   TEXT NOT NULL            -- ISO 8601 UTC with milliseconds
);

-- Primary access pattern: get changes since version X
CREATE INDEX idx_sync_log_version ON _sync_log(version);

-- Secondary: table-specific queries
CREATE INDEX idx_sync_log_table ON _sync_log(table_name, version);
```

### 8.3 JSON Payload Serialization

Payloads are generated by triggers using the database's native JSON functions. The trigger generator MUST enumerate all columns at generation time.

**Column Enumeration:** The code generator reads table metadata (column names, types) and generates triggers with explicit column lists. When schema changes, triggers MUST be regenerated.

```sql
-- Generated trigger knows all columns at generation time
json_object('Id', NEW.Id, 'Name', NEW.Name, 'Email', NEW.Email, 'CreatedAt', NEW.CreatedAt)
```

This is acceptable because:
- Triggers are generated code, regenerated on schema change
- Explicit columns prevent accidentally syncing sensitive columns
- Failed triggers on missing columns surface schema drift immediately

---

## 9. Trigger Suppression

### 9.1 The Problem

When applying incoming changes, local triggers fire and log those changes again. This creates:
- Duplicate entries in the log
- Infinite sync loops
- Wasted storage and bandwidth

### 9.2 Solution: Sync Session Flag

```sql
CREATE TABLE _sync_session (
    sync_active INTEGER DEFAULT 0
);
INSERT INTO _sync_session VALUES (0);
```

Triggers check suppression and apply data mapping (Section 7) during sync:

```sql
CREATE TRIGGER Person_sync_insert
AFTER INSERT ON Person
WHEN (SELECT sync_active FROM _sync_session) = 0
BEGIN
    INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
    VALUES ('Person', json_object('Id', NEW.Id), 'insert',
            json_object('Id', NEW.Id, 'Name', NEW.Name, 'Email', NEW.Email),
            (SELECT value FROM _sync_state WHERE key = 'origin_id'),
            strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));
END;
```

### 9.3 Sync Application Pattern

```sql
UPDATE _sync_session SET sync_active = 1;  -- Enable suppression
BEGIN TRANSACTION;
-- Apply changes with mapping transforms (Section 7)
COMMIT;
UPDATE _sync_session SET sync_active = 0;  -- Disable suppression
```

### 9.4 Requirements

- Trigger suppression MUST be enabled before applying incoming changes
- Data mapping (Section 7) executes AFTER suppression enabled, BEFORE writes

---

## 10. LQL Trigger Generation

### 10.1 Overview

Triggers are defined in LQL (Lambda Query Language) and transpiled to platform-specific SQL. This enables a single trigger definition to work across SQLite, SQL Server, and PostgreSQL.

### 10.2 LQL Trigger Syntax

```lql
Person
|> syncTriggers({ columns: ["Id", "Name", "Email"], pkColumn: "Id" })
```

Generates INSERT, UPDATE, DELETE triggers that check suppression, serialize to JSON, include origin ID.

### 10.3 Column Enumeration

| Platform | Metadata Source |
|----------|-----------------|
| SQLite | `PRAGMA table_info(TableName)` |
| SQL Server | `INFORMATION_SCHEMA.COLUMNS` |
| PostgreSQL | `information_schema.columns` |

### 10.4 Requirements

- Triggers MUST be generated via LQL transpilation
- Each tracked table MUST have INSERT, UPDATE, DELETE triggers
- Triggers MUST be regenerated when schema changes
- Data mapping (Section 7) is applied at sync time, NOT trigger time

---

## 11. Real-Time Subscriptions

### 11.1 Overview

Clients can subscribe to real-time change notifications from the server. This complements batch sync with immediate push notifications for active clients.

### 11.2 Subscription Types

| Type | Description |
|------|-------------|
| **Record** | Subscribe to specific PK(s) |
| **Table** | Subscribe to all changes in a table |
| **Query** | Subscribe to records matching criteria |

### 11.3 Notifications

Notifications include **mapped** payloads (per Section 7) if mapping is configured:

```json
{
    "type": "change",
    "subscription_id": "sub-uuid-123",
    "change": {
        "version": 12345,
        "table_name": "Orders",
        "pk_value": {"Id": "order-uuid-456"},
        "operation": "update",
        "payload": {"Id": "order-uuid-456", "Status": "shipped"},
        "origin": "origin-uuid-999",
        "timestamp": "2025-12-18T10:30:00.123Z"
    }
}
```

### 11.4 Requirements

- At-least-once delivery; clients handle idempotently
- Missed notifications recovered via batch sync
- Mapping (Section 7) applies to notification payloads

---

## 12. Bi-Directional Sync Protocol

### 12.1 Sync Phases

1. **Pull**: Get changes from server, **apply mapping** (Section 7), write locally
2. **Push**: Collect local changes, **apply mapping**, send to server

### 12.2 Sync Session Protocol

```
PULL PHASE:
1. Enable trigger suppression (Section 9)
2. For each batch:
   a. Fetch batch from server
   b. Apply data mapping (Section 7) to each change
   c. Apply mapped changes, deferring FK violations
   d. Retry deferred (max 3 passes)
   e. Update sync tracking state
3. Disable trigger suppression

PUSH PHASE:
1. Collect local changes since last push
2. Apply push mapping (Section 7)
3. Send mapped changes to server in batches
4. Update sync tracking
```

### 12.3 Requirements

- Changes applied in global version order
- FK violations deferred, not failed
- **Mapping (Section 7) applied per-change before write**
- Per-batch transactions for million-record syncs

---

## 13. Batching

### 13.1 Batch Query

```sql
SELECT version, table_name, pk_value, operation, payload, origin, timestamp
FROM _sync_log
WHERE version > @from_version
ORDER BY version ASC
LIMIT @batch_size;
```

### 13.2 Batch Processing with Mapping

For each change in batch:

1. **Look up mapping** (Section 7) for source table
2. **Apply mapping transforms** (LQL or config)
3. Execute operation on **mapped target table(s)**:
   - `insert`: INSERT or UPSERT
   - `update`: UPDATE (or UPSERT)
   - `delete`: DELETE
4. On FK violation: defer to retry queue
5. Update sync tracking per mapping

---

## 14. Tombstone Retention

### 14.1 The Problem

Deleted records must be tracked so replicas know to delete them too. But keeping tombstones forever wastes storage.

### 14.2 Retention Strategy

Tombstones retained until all active clients have synced past them.

```sql
CREATE TABLE _sync_clients (
    origin_id TEXT PRIMARY KEY,
    last_sync_version INTEGER NOT NULL,
    last_sync_timestamp TEXT NOT NULL
);

-- Safe purge: minimum version all clients have synced past
SELECT MIN(last_sync_version) AS safe_purge_version FROM _sync_clients;
```

### 14.3 Requirements

- Tombstones MUST NOT be purged until all clients synced past
- Clients missing tombstones MUST perform full resync
- Mapping (Section 7) applies to delete operations too

---

## 15. Conflict Resolution

### 15.1 Conflict Detection

Conflicts occur on same **mapped target** table/pk with different origins.

### 15.2 Resolution Strategies

| Strategy | Description |
|----------|-------------|
| **Last-Write-Wins** | Highest timestamp wins (default) |
| **Server Wins** | Server version always wins |
| **Client Wins** | Client version always wins |
| **Custom** | LQL expression per mapping |

### 15.3 Requirements

- Conflicts detected on **target** pk after mapping (Section 7)
- Echo prevention: `WHERE origin != @my_origin_id`

---

## 16. Hash Verification

### 16.1 Purpose

Hash verification ensures database consistency. If two replicas have the same data, they produce the same hash.

### 16.2 Canonical JSON

Keys sorted alphabetically, no whitespace, UTF-8 encoded.

```json
{"Email":"alice@example.com","Id":"550e8400-e29b-41d4-a716-446655440000","Name":"Alice"}
```

### 16.3 Hash Algorithms

**Full Database Hash**: SHA-256 of all tables/rows in sorted order.

**Batch Hash**: SHA-256 of `{version}:{table_name}:{pk_value}:{operation}:{payload}\n` per change.

---

## 17. Security Considerations

- Mapping (Section 7) `excluded_columns` enforces sensitive data exclusion
- Change log MUST NOT store secrets beyond base tables
- Sync endpoints MUST authenticate before accepting changes

---

## 18. Schema Metadata

### 18.1 Overview

Schema metadata describes **both source and target** schemas for mapping validation.

### 18.2 Schema JSON Format

```json
{
    "version": "1.0",
    "source_schema": {
        "tables": [
            {
                "name": "User",
                "pk_column": "Id",
                "columns": [
                    {"name": "Id", "type": "uuid"},
                    {"name": "FullName", "type": "string"},
                    {"name": "PasswordHash", "type": "string"}
                ]
            }
        ]
    },
    "target_schema": {
        "tables": [
            {
                "name": "Customer",
                "pk_column": "CustomerId",
                "columns": [
                    {"name": "CustomerId", "type": "uuid"},
                    {"name": "Name", "type": "string"}
                ]
            }
        ]
    },
    "mappings": [
        {
            "source_table": "User",
            "target_table": "Customer",
            "excluded_columns": ["PasswordHash"]
        }
    ]
}
```

### 18.3 Column Type Mappings

Portable type identifiers map to platform-specific types:

| Portable Type | SQLite | SQL Server | PostgreSQL |
|---------------|--------|------------|------------|
| `uuid` | TEXT | UNIQUEIDENTIFIER | UUID |
| `string` | TEXT | NVARCHAR(n) | VARCHAR(n) |
| `int` | INTEGER | INT | INTEGER |
| `bigint` | INTEGER | BIGINT | BIGINT |
| `decimal` | REAL | DECIMAL(p,s) | NUMERIC(p,s) |
| `boolean` | INTEGER | BIT | BOOLEAN |
| `datetime` | TEXT | DATETIME2 | TIMESTAMPTZ |
| `blob` | BLOB | VARBINARY(MAX) | BYTEA |

### 18.4 Requirements

- Schema metadata MUST include **source and target** table definitions
- Mapping config MUST be included in schema exchange
- Schema changes trigger mapping and trigger regeneration
- Validate mapped column types are compatible

---

## 19. Conformance Requirements

An implementation is **conformant** if:

1. All tracked tables have single-column primary keys
2. Origin ID is generated as UUID v4 and stored permanently
3. All timestamps are UTC ISO 8601 with milliseconds
4. Triggers check suppression flag before logging
5. Triggers include origin ID in every entry
6. Changes are applied in global version order with FK defer/retry
7. Tombstones are retained until all clients have synced past them
8. Hash computation follows canonical JSON specification
9. Batching uses per-batch transactions with version checkpointing
10. Triggers are generated via LQL transpilation
11. Server supports real-time subscriptions (record and table level)
12. Schema metadata is available in standard JSON format
13. **Data mappings defined via JSON config (simple) or LQL (complex)**
14. **Sync tracking prevents re-syncing unchanged records**
15. **Mappings support directional control (push/pull/both)**

---

## 20. Appendices

### Appendix A: Complete Schema

```sql
-- Sync state (persistent)
CREATE TABLE _sync_state (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

-- Sync session (ephemeral flag)
CREATE TABLE _sync_session (
    sync_active INTEGER DEFAULT 0
);
INSERT INTO _sync_session VALUES (0);

-- Change log
CREATE TABLE _sync_log (
    version     INTEGER PRIMARY KEY AUTOINCREMENT,
    table_name  TEXT NOT NULL,
    pk_value    TEXT NOT NULL,
    operation   TEXT NOT NULL CHECK (operation IN ('insert', 'update', 'delete')),
    payload     TEXT,
    origin      TEXT NOT NULL,
    timestamp   TEXT NOT NULL
);

CREATE INDEX idx_sync_log_version ON _sync_log(version);
CREATE INDEX idx_sync_log_table ON _sync_log(table_name, version);

-- Initialize
INSERT INTO _sync_state VALUES ('origin_id', '');  -- Set on first sync init
INSERT INTO _sync_state VALUES ('last_server_version', '0');
INSERT INTO _sync_state VALUES ('last_push_version', '0');

```

### Appendix B: Server Client Tracking (for tombstone retention)

```sql
CREATE TABLE _sync_clients (
    origin_id TEXT PRIMARY KEY,
    last_sync_version INTEGER NOT NULL DEFAULT 0,
    last_sync_timestamp TEXT NOT NULL,
    created_at TEXT NOT NULL
);
```

### Appendix C: Platform-Specific Notes

**SQLite:**
- `json_object()` available in SQLite 3.38+ (2022)
- `strftime('%Y-%m-%dT%H:%M:%fZ', 'now')` for timestamps
- `WHEN` clause on triggers for suppression

**SQL Server:**
- Can use native Change Tracking instead of triggers
- `FOR JSON` for payload serialization
- Session context or temp table for suppression flag

**PostgreSQL:**
- `jsonb_build_object()` for payloads
- `current_setting()` for session variables (suppression)
- Consider `pg_logical` for change capture

---

## 21. References

- [SQLite CREATE TRIGGER](https://sqlite.org/lang_createtrigger.html)
- [SQLite JSON Functions](https://sqlite.org/json1.html)
- [RFC 4122 - UUID](https://tools.ietf.org/html/rfc4122)
- [RFC 8785 - JSON Canonicalization](https://tools.ietf.org/html/rfc8785)
- [Dotmim.Sync Documentation](https://dotmimsync.readthedocs.io/)
