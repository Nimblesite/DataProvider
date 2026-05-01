# Row-Level Security (RLS) Migration Specification

## Reference Documentation

- [PostgreSQL Row Security Policies](https://www.postgresql.org/docs/current/ddl-rowsecurity.html)
- [SQL Server Row-Level Security](https://learn.microsoft.com/en-us/sql/relational-databases/security/row-level-security?view=sql-server-ver17)
- [SQLite CREATE TRIGGER](https://www.sqlite.org/lang_createtrigger.html)

---

## 1. Introduction [RLS-INTRO]

Row-Level Security restricts which rows a database user can see or modify, enforced at the database engine level rather than the application layer. This is critical for multi-tenant systems, healthcare data (HIPAA/FHIR), and any scenario where row ownership or group membership controls access.

### 1.1 Platform Differences [RLS-INTRO-PLATFORMS]

| Platform | Native RLS | Mechanism |
|---|---|---|
| PostgreSQL | Yes | `CREATE POLICY` + `ALTER TABLE ... ENABLE ROW LEVEL SECURITY` |
| SQL Server | Yes | `CREATE SECURITY POLICY` with inline-table-valued predicate functions |
| SQLite | No | Emulated with `BEFORE INSERT/UPDATE/DELETE` triggers; SELECT filtering via generated views |

### 1.2 Owner Rule [RLS-INTRO-OWNER]

Every RLS policy defined in YAML MUST produce correct, enforced access control on every supported backend. No "silently ignored" policies. A policy on a backend that cannot enforce it is a security hole -- emit a hard error, not a warning.

### 1.3 LQL Requirement [RLS-INTRO-LQL]

All policy predicates that query other tables (e.g. group membership checks) MUST be expressed in LQL, which is transpiled to platform-specific SQL at migration time. Simple single-table predicates (e.g. `OwnerId = current_user_id()`) also use LQL predicate syntax.

---

## 2. Session Context [RLS-CONTEXT-SESSION]

RLS predicates need access to the identity of the current user. Each platform has a different mechanism. The LQL function `current_user_id()` is the canonical way to reference it in policy predicates:

| Platform | LQL `current_user_id()` transpiles to |
|---|---|
| PostgreSQL | `current_setting('rls.current_user_id', true)` |
| SQL Server | `CAST(SESSION_CONTEXT(N'current_user_id') AS NVARCHAR(450))` |
| SQLite | `(SELECT current_user_id FROM [__rls_context] LIMIT 1)` |

Application code is responsible for setting the session context before executing queries:
- **PostgreSQL**: `SET LOCAL rls.current_user_id = '...';` (within a transaction)
- **SQL Server**: `EXEC sp_set_session_context N'current_user_id', N'...';`
- **SQLite**: `INSERT OR REPLACE INTO [__rls_context](current_user_id) VALUES ('...');`

The `__rls_context` table in SQLite is a single-row shadow table auto-generated whenever any RLS policy exists in the schema. It is treated as a system table and not included in `SchemaDefinition.Tables`.

---

## 3. Policy Definition Model [RLS-CORE-POLICY]

```csharp
public sealed record RlsPolicySetDefinition
{
    public bool Enabled { get; init; } = true;
    public IReadOnlyList<RlsPolicyDefinition> Policies { get; init; } = [];
}

public sealed record RlsPolicyDefinition
{
    public string Name { get; init; } = string.Empty;
    public bool IsPermissive { get; init; } = true;   // PERMISSIVE vs RESTRICTIVE
    public IReadOnlyList<RlsOperation> Operations { get; init; } = [RlsOperation.All];
    public IReadOnlyList<string> Roles { get; init; } = [];  // empty = all roles
    // LQL predicate for USING clause (applied to SELECT, UPDATE-existing, DELETE)
    public string? UsingLql { get; init; }
    // LQL predicate for WITH CHECK clause (applied to INSERT, UPDATE-new)
    public string? WithCheckLql { get; init; }
}

public enum RlsOperation { All, Select, Insert, Update, Delete }
```

`TableDefinition` gains one new property:

```csharp
public RlsPolicySetDefinition? RowLevelSecurity { get; init; }
```

---

## 4. LQL Predicate Transpilation [RLS-CORE-LQL]

The `UsingLql` and `WithCheckLql` strings are LQL predicate expressions. They are transpiled to platform-specific SQL at migration time (DDL generation) by `RlsPredicateTranspiler`.

### 4.1 Simple Predicates [RLS-CORE-LQL-SIMPLE]

No subquery, uses `current_user_id()` or column comparisons:
```
OwnerId = current_user_id()
```
Translated by `RlsPredicateTranspiler.Translate(lql, platform)`, substituting `current_user_id()` per platform, quoting identifiers appropriately.

### 4.2 Subquery Predicates [RLS-CORE-LQL-SUBQUERY]

Group membership, hierarchy checks -- MUST use LQL:
```
exists(
  UserGroupMemberships
  |> filter(fn(m) => m.UserId = current_user_id() and m.GroupId = GroupId)
)
```
Translated by invoking `Nimblesite.Lql.Core.LqlStatementConverter.ToStatement()` on the inner LQL and then calling the platform-specific transpiler (`ToPostgreSql()`, `ToSQLite()`, `ToSqlServer()`). The `exists()` wrapper is handled by `RlsPredicateTranspiler` surrounding the transpiled query with `EXISTS (...)`.

`current_user_id()` inside LQL subqueries is substituted **after** LQL transpilation by string replacement of a sentinel placeholder.

### 4.3 Transpiler Contract [RLS-CORE-LQL-CONTRACT]

`RlsPredicateTranspiler` must:
- Return `Result<string, MigrationError>` -- no throwing
- Validate that `UsingLql` / `WithCheckLql` are non-empty when policies include operations that require them
- Emit `MIG-E-RLS-EMPTY-PREDICATE` when a USING clause is missing for SELECT/UPDATE/DELETE

---

## 5. Schema Operations [RLS-CORE-OPS]

Added to `SchemaOperation.cs` (closed discriminated union):

```csharp
// Enable RLS on a table (additive)
public sealed record EnableRlsOperation(string Schema, string TableName) : SchemaOperation;
// Create a policy (additive)
public sealed record CreateRlsPolicyOperation(
    string Schema, string TableName, RlsPolicyDefinition Policy
) : SchemaOperation;
// DESTRUCTIVE -- require opt-in
public sealed record DropRlsPolicyOperation(
    string Schema, string TableName, string PolicyName
) : SchemaOperation;
public sealed record DisableRlsOperation(string Schema, string TableName) : SchemaOperation;
```

`MigrationRunner.IsDestructive` extended to return `true` for `DropRlsPolicyOperation` and `DisableRlsOperation`.

---

## 6. YAML Format [RLS-YAML]

```yaml
name: MyApp
tables:
  - name: Documents
    schema: public
    columns:
      - name: Id
        type: Uuid
        isNullable: false
      - name: OwnerId
        type: Uuid
        isNullable: false
      - name: GroupId
        type: Uuid
        isNullable: true
    primaryKey:
      columns: [Id]
    rowLevelSecurity:
      enabled: true
      policies:
        - name: owner_isolation
          permissive: true
          operations: [All]
          roles: []
          using: "OwnerId = current_user_id()"
          withCheck: "OwnerId = current_user_id()"
        - name: group_read_access
          permissive: true
          operations: [Select]
          using: |
            exists(
              UserGroupMemberships
              |> filter(fn(m) => m.UserId = current_user_id() and m.GroupId = GroupId)
            )
```

The `SchemaYamlSerializer` requires a new `RlsPolicySetDefinitionYamlConverter` and type mappings for `IReadOnlyList<RlsPolicyDefinition>` and `IReadOnlyList<RlsOperation>`.

---

## 7. PostgreSQL Implementation [RLS-PG]

**`EnableRlsOperation`** -> `ALTER TABLE "schema"."table" ENABLE ROW LEVEL SECURITY`

**`CreateRlsPolicyOperation`** -> full `CREATE POLICY`:

```sql
-- Implements [RLS-PG]
CREATE POLICY "owner_isolation" ON "public"."Documents"
  AS PERMISSIVE
  FOR ALL
  TO PUBLIC
  USING (current_setting('rls.current_user_id', true) = "OwnerId")
  WITH CHECK (current_setting('rls.current_user_id', true) = "OwnerId");
```

Group membership check (LQL subquery transpiled):
```sql
CREATE POLICY "group_read_access" ON "public"."Documents"
  AS PERMISSIVE
  FOR SELECT
  TO PUBLIC
  USING (
    EXISTS (
      SELECT 1 FROM "public"."UserGroupMemberships"
      WHERE "UserId" = current_setting('rls.current_user_id', true)
        AND "GroupId" = "Documents"."GroupId"
    )
  );
```

**`DropRlsPolicyOperation`** -> `DROP POLICY IF EXISTS "name" ON "schema"."table"`

**`DisableRlsOperation`** -> `ALTER TABLE "schema"."table" DISABLE ROW LEVEL SECURITY`

Generation order per table: `EnableRlsOperation` always precedes `CreateRlsPolicyOperation`.

---

## 8. SQL Server Implementation [RLS-MSSQL]

> **Status:** SQL Server package (`Nimblesite.DataProvider.Migration.SqlServer`) does not exist yet. This section defines the contract; implementation is deferred until that package ships. Any `CreateRlsPolicyOperation` targeting SQL Server before the package exists MUST emit `MIG-E-RLS-MSSQL-UNSUPPORTED`.

SQL Server RLS uses a two-step approach:
1. An inline table-valued function (iTVF) as a filter or block predicate
2. `CREATE SECURITY POLICY` binding that iTVF to the table

```sql
-- Filter predicate function -- Implements [RLS-MSSQL-FUNC]
CREATE FUNCTION dbo.fn_rls_owner_isolation_Documents(@OwnerId NVARCHAR(450))
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN SELECT 1 AS fn_result
WHERE CAST(SESSION_CONTEXT(N'current_user_id') AS NVARCHAR(450)) = @OwnerId;
GO

-- Security policy -- Implements [RLS-MSSQL-POLICY]
CREATE SECURITY POLICY dbo.pol_Documents
  ADD FILTER PREDICATE dbo.fn_rls_owner_isolation_Documents([OwnerId]) ON dbo.Documents,
  ADD BLOCK PREDICATE dbo.fn_rls_owner_isolation_Documents([OwnerId]) ON dbo.Documents AFTER INSERT
WITH (STATE = ON);
```

For subquery predicates (group membership), the iTVF body joins or references the membership table. LQL is transpiled to the iTVF `RETURN SELECT ... WHERE` expression.

---

## 9. SQLite Trigger Emulation [RLS-SQLITE]

SQLite has no native RLS. The emulation strategy:

| Policy operation | Emulation mechanism |
|---|---|
| INSERT protection | `BEFORE INSERT` trigger with `RAISE(ABORT, ...)` |
| UPDATE protection | `BEFORE UPDATE` trigger with `RAISE(ABORT, ...)` (checks NEW row) |
| DELETE protection | `BEFORE DELETE` trigger with `RAISE(ABORT, ...)` (checks OLD row) |
| SELECT filtering | Auto-generated `CREATE VIEW {Table}_secure AS SELECT ... WHERE predicate` |

### 9.1 Context Table [RLS-SQLITE-CONTEXT]

`EnableRlsOperation` on SQLite generates the `__rls_context` system table:

```sql
CREATE TABLE IF NOT EXISTS [__rls_context] (
  [current_user_id] TEXT NOT NULL
);
```

### 9.2 DML Triggers [RLS-SQLITE-DML]

**INSERT** (simple ownership predicate):
```sql
CREATE TRIGGER IF NOT EXISTS [rls_insert_owner_isolation_Documents]
BEFORE INSERT ON [Documents]
BEGIN
  SELECT RAISE(ABORT, 'RLS-SQLITE: access denied [owner_isolation]')
  WHERE NEW.[OwnerId] != (SELECT current_user_id FROM [__rls_context] LIMIT 1);
END;
```

**UPDATE** (LQL `withCheck` applied to NEW row):
```sql
CREATE TRIGGER IF NOT EXISTS [rls_update_owner_isolation_Documents]
BEFORE UPDATE ON [Documents]
BEGIN
  SELECT RAISE(ABORT, 'RLS-SQLITE: access denied [owner_isolation]')
  WHERE NEW.[OwnerId] != (SELECT current_user_id FROM [__rls_context] LIMIT 1);
END;
```

**DELETE** (LQL `using` applied to OLD row):
```sql
CREATE TRIGGER IF NOT EXISTS [rls_delete_owner_isolation_Documents]
BEFORE DELETE ON [Documents]
BEGIN
  SELECT RAISE(ABORT, 'RLS-SQLITE: access denied [owner_isolation]')
  WHERE OLD.[OwnerId] != (SELECT current_user_id FROM [__rls_context] LIMIT 1);
END;
```

### 9.3 Group Membership Subquery [RLS-SQLITE-DML-SUBQUERY]

LQL transpiled to SQLite subquery inside trigger:

```sql
CREATE TRIGGER IF NOT EXISTS [rls_delete_group_read_access_Documents]
BEFORE DELETE ON [Documents]
BEGIN
  SELECT RAISE(ABORT, 'RLS-SQLITE: access denied [group_read_access]')
  WHERE NOT EXISTS (
    SELECT 1 FROM [UserGroupMemberships]
    WHERE [UserId] = (SELECT current_user_id FROM [__rls_context] LIMIT 1)
      AND [GroupId] = OLD.[GroupId]
  );
END;
```

### 9.4 SELECT Emulation [RLS-SQLITE-SELECT]

```sql
CREATE VIEW IF NOT EXISTS [Documents_secure] AS
SELECT * FROM [Documents]
WHERE [OwnerId] = (SELECT current_user_id FROM [__rls_context] LIMIT 1);
```

> **Limitation:** SQLite triggers do not intercept `SELECT` queries. The generated `_secure` view filters SELECT results. Applications MUST query `{TableName}_secure` for row-level SELECT enforcement on SQLite. The migration tool logs `MIG-W-RLS-SQLITE-SELECT-VIEW` to document this requirement.

### 9.5 Policy Combination [RLS-SQLITE-COMBINE]

Multiple policies on the same table combine as AND conditions inside a single trigger. If RESTRICTIVE policies are present, emit `MIG-W-RLS-SQLITE-RESTRICTIVE-APPROX` because SQLite cannot distinguish permissive/restrictive -- all conditions are ANDed.

**Trigger naming convention**: `rls_{operation}_{policyName}_{TableName}`

---

## 10. Schema Diff [RLS-DIFF]

`SchemaDiff.Calculate` gains RLS diff logic comparing `TableDefinition.RowLevelSecurity` between current and desired schemas:

- Table exists in desired with RLS enabled but not in current -> emit `EnableRlsOperation` then `CreateRlsPolicyOperation` for each policy
- Policy in desired not in current -> emit `CreateRlsPolicyOperation`
- Policy in current but not in desired (with `allowDestructive: true`) -> emit `DropRlsPolicyOperation`
- RLS disabled in desired but enabled in current (with `allowDestructive: true`) -> emit `DisableRlsOperation`

Schema inspectors (`PostgresSchemaInspector`, `SqliteSchemaInspector`) must be extended to read existing policies/triggers back into `RlsPolicySetDefinition` for accurate diffs. On SQLite, existing `rls_*` triggers are reverse-mapped. On Postgres, `pg_policies` system view is queried.

---

## 11. Error / Warning Codes [RLS-ERRORS]

| Code | Meaning |
|---|---|
| `MIG-E-RLS-EMPTY-PREDICATE` | Policy has SELECT/UPDATE/DELETE operations but no `UsingLql` |
| `MIG-E-RLS-EMPTY-CHECK` | Policy has INSERT/UPDATE operations but no `WithCheckLql` |
| `MIG-E-RLS-LQL-PARSE` | `UsingLql` / `WithCheckLql` failed LQL parse |
| `MIG-E-RLS-LQL-TRANSPILE` | LQL transpilation to platform SQL failed |
| `MIG-E-RLS-MSSQL-UNSUPPORTED` | SQL Server RLS attempted before `SqlServer` package ships |
| `MIG-W-RLS-SQLITE-SELECT-VIEW` | Informational: SELECT policy enforced via `_secure` view, not triggers |
| `MIG-W-RLS-SQLITE-RESTRICTIVE-APPROX` | RESTRICTIVE policy approximated as AND condition in SQLite triggers |

---

## 12. Testing Requirements [RLS-TEST]

All tests follow CLAUDE.md protocol: write failing test first, verify failure, implement, verify pass.

### 12.1 Core / YAML

- `RlsPolicyDefinition_YamlRoundTrip_Simple`
- `RlsPolicyDefinition_YamlRoundTrip_SubqueryPolicy`
- `RlsOperation_AllValues_SerializeDeserialize`
- `RlsPolicy_MissingUsingLql_EmitsError`
- `RlsPredicateTranspiler_CurrentUserId_Postgres` / `_Sqlite` / `_SqlServer`
- `RlsPredicateTranspiler_ExistsSubquery_Postgres` / `_Sqlite`
- `RlsPredicateTranspiler_LqlParseFailure_EmitsError`

### 12.2 PostgreSQL E2E (testcontainer)

- `Postgres_EnableRls_TableHasRls`
- `Postgres_CreatePolicy_OwnerIsolation_BlocksCrossUserAccess`
- `Postgres_CreatePolicy_GroupMembership_LqlSubquery_AllowsGroupMemberAccess`
- `Postgres_SchemaDiff_AddsNewPolicy`
- `Postgres_SchemaDiff_AllowDestructive_DropsPolicy`
- `Postgres_SchemaInspector_ReadsBackPolicies`

### 12.3 SQLite E2E

- `Sqlite_EnableRls_CreatesRlsContextTable`
- `Sqlite_CreatePolicy_Insert_TriggerBlocksCrossOwnerInsert`
- `Sqlite_CreatePolicy_Update_TriggerBlocksCrossOwnerUpdate`
- `Sqlite_CreatePolicy_Delete_TriggerBlocksCrossOwnerDelete`
- `Sqlite_CreatePolicy_GroupMembership_TriggerUsesSubquery`
- `Sqlite_SelectPolicy_CreatesSecureView`
- `Sqlite_SchemaInspector_ReadsBackTriggers`
- `Sqlite_RestrictivePolicy_EmitsWarning`
