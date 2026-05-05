namespace Nimblesite.DataProvider.Migration.Core;

/// <summary>
/// Base type for schema migration operations.
/// Pattern match to get specific operation details.
/// </summary>
public abstract record SchemaOperation
{
    /// <summary>
    /// Prevents external inheritance - this makes the type hierarchy "closed".
    /// </summary>
    private protected SchemaOperation() { }
}

/// <summary>
/// Create a new table.
/// </summary>
public sealed record CreateTableOperation(TableDefinition Table) : SchemaOperation;

/// <summary>
/// Add a column to an existing table.
/// </summary>
public sealed record AddColumnOperation(string Schema, string TableName, ColumnDefinition Column)
    : SchemaOperation;

/// <summary>
/// Create an index on a table.
/// </summary>
public sealed record CreateIndexOperation(string Schema, string TableName, IndexDefinition Index)
    : SchemaOperation;

/// <summary>
/// Add a foreign key constraint.
/// </summary>
public sealed record AddForeignKeyOperation(
    string Schema,
    string TableName,
    ForeignKeyDefinition ForeignKey
) : SchemaOperation;

/// <summary>
/// Add a check constraint.
/// </summary>
public sealed record AddCheckConstraintOperation(
    string Schema,
    string TableName,
    CheckConstraintDefinition CheckConstraint
) : SchemaOperation;

/// <summary>
/// Add a unique constraint.
/// </summary>
public sealed record AddUniqueConstraintOperation(
    string Schema,
    string TableName,
    UniqueConstraintDefinition UniqueConstraint
) : SchemaOperation;

// ═══════════════════════════════════════════════════════════════════
// POSTGRES SUPPORT OBJECTS - Implements [RLS-PG-SUPPORT-DDL]
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Create or alter a PostgreSQL role and its declared membership grants.
/// </summary>
public sealed record CreateOrAlterRoleOperation(PostgresRoleDefinition Role) : SchemaOperation;

/// <summary>
/// Create or replace a PostgreSQL function and declared EXECUTE grants.
/// </summary>
public sealed record CreateOrReplaceFunctionOperation(PostgresFunctionDefinition Function)
    : SchemaOperation;

/// <summary>
/// Apply a PostgreSQL schema or table grant.
/// </summary>
public sealed record GrantPrivilegesOperation(PostgresGrantDefinition Grant) : SchemaOperation;

// ═══════════════════════════════════════════════════════════════════
// ROW-LEVEL SECURITY - Implements [RLS-CORE-OPS]
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Enable row-level security on a table. Additive.
/// </summary>
public sealed record EnableRlsOperation(string Schema, string TableName) : SchemaOperation;

/// <summary>
/// Set <c>FORCE ROW LEVEL SECURITY</c> on a table so RLS applies to the
/// table owner too. Postgres-only. Implements GitHub issue #37.
/// </summary>
public sealed record EnableForceRlsOperation(string Schema, string TableName) : SchemaOperation;

/// <summary>
/// Create a row-level security policy. Additive.
/// </summary>
public sealed record CreateRlsPolicyOperation(
    string Schema,
    string TableName,
    RlsPolicyDefinition Policy
) : SchemaOperation;

// ═══════════════════════════════════════════════════════════════════
// DESTRUCTIVE OPERATIONS - Require explicit opt-in
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Drop a table. DESTRUCTIVE - requires explicit opt-in.
/// </summary>
public sealed record DropTableOperation(string Schema, string TableName) : SchemaOperation;

/// <summary>
/// Drop a column. DESTRUCTIVE - requires explicit opt-in.
/// </summary>
public sealed record DropColumnOperation(string Schema, string TableName, string ColumnName)
    : SchemaOperation;

/// <summary>
/// Drop an index.
/// </summary>
public sealed record DropIndexOperation(string Schema, string TableName, string IndexName)
    : SchemaOperation;

/// <summary>
/// Drop a foreign key constraint.
/// </summary>
public sealed record DropForeignKeyOperation(string Schema, string TableName, string ConstraintName)
    : SchemaOperation;

/// <summary>
/// Drop a PostgreSQL function. DESTRUCTIVE - requires explicit opt-in.
/// </summary>
public sealed record DropFunctionOperation(
    string Schema,
    string Name,
    IReadOnlyList<string> ArgumentTypes
) : SchemaOperation;

/// <summary>
/// Revoke a PostgreSQL schema or table grant. DESTRUCTIVE - requires explicit opt-in.
/// </summary>
public sealed record RevokePrivilegesOperation(PostgresGrantDefinition Grant) : SchemaOperation;

/// <summary>
/// Drop a row-level security policy. DESTRUCTIVE - requires explicit opt-in.
/// </summary>
public sealed record DropRlsPolicyOperation(string Schema, string TableName, string PolicyName)
    : SchemaOperation;

/// <summary>
/// Disable row-level security on a table. DESTRUCTIVE - requires explicit
/// opt-in because rows previously hidden by policies become visible.
/// </summary>
public sealed record DisableRlsOperation(string Schema, string TableName) : SchemaOperation;

/// <summary>
/// Drop <c>FORCE ROW LEVEL SECURITY</c> -- weakens enforcement (table owner
/// regains bypass). DESTRUCTIVE. Implements GitHub issue #37.
/// </summary>
public sealed record DisableForceRlsOperation(string Schema, string TableName) : SchemaOperation;
