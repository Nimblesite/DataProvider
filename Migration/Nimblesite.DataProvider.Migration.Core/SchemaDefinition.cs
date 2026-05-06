namespace Nimblesite.DataProvider.Migration.Core;

/// <summary>
/// Complete database schema definition.
/// </summary>
public sealed record SchemaDefinition
{
    /// <summary>Schema name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Tables in this schema.</summary>
    public IReadOnlyList<TableDefinition> Tables { get; init; } = [];

    /// <summary>
    /// PostgreSQL roles managed by this schema. Implements [RLS-PG-SUPPORT-DDL].
    /// </summary>
    public IReadOnlyList<PostgresRoleDefinition> Roles { get; init; } = [];

    /// <summary>
    /// PostgreSQL helper functions managed by this schema. Implements [RLS-PG-SUPPORT-DDL].
    /// </summary>
    public IReadOnlyList<PostgresFunctionDefinition> Functions { get; init; } = [];

    /// <summary>
    /// PostgreSQL grants managed by this schema. Implements [RLS-PG-SUPPORT-DDL].
    /// </summary>
    public IReadOnlyList<PostgresGrantDefinition> Grants { get; init; } = [];
}

/// <summary>
/// PostgreSQL role definition for migration-managed application roles.
/// Implements [RLS-PG-SUPPORT-DDL].
/// </summary>
public sealed record PostgresRoleDefinition
{
    /// <summary>Role name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Whether the role can log in directly.</summary>
    public bool Login { get; init; }

    /// <summary>Whether the role can bypass row-level security.</summary>
    public bool BypassRls { get; init; }

    /// <summary>Roles or users that receive membership in this role.</summary>
    public IReadOnlyList<string> GrantTo { get; init; } = [];
}

/// <summary>
/// PostgreSQL SQL-language function definition for RLS helper functions.
/// Implements [RLS-PG-SUPPORT-DDL].
/// </summary>
public sealed record PostgresFunctionDefinition
{
    /// <summary>Function schema.</summary>
    public string Schema { get; init; } = "public";

    /// <summary>Function name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Function arguments in declaration order.</summary>
    public IReadOnlyList<PostgresFunctionArgumentDefinition> Arguments { get; init; } = [];

    /// <summary>PostgreSQL return type, such as <c>uuid</c> or <c>boolean</c>.</summary>
    public string Returns { get; init; } = "void";

    /// <summary>Function language. NAP RLS helpers use <c>sql</c>.</summary>
    public string Language { get; init; } = "sql";

    /// <summary>PostgreSQL volatility keyword: <c>volatile</c>, <c>stable</c>, or <c>immutable</c>.</summary>
    public string Volatility { get; init; } = "stable";

    /// <summary>Whether to emit <c>SECURITY DEFINER</c>.</summary>
    public bool SecurityDefiner { get; init; }

    /// <summary>Function body placed between PostgreSQL dollar quotes.</summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>
    /// LQL function body expression. Mutually exclusive with <see cref="Body" />.
    /// Emits a SQL-language function body for PostgreSQL.
    /// </summary>
    public string? BodyLql { get; init; }

    /// <summary>Roles granted EXECUTE on this function.</summary>
    public IReadOnlyList<string> ExecuteRoles { get; init; } = [];

    /// <summary>Whether PUBLIC execute must be revoked.</summary>
    public bool RevokePublicExecute { get; init; } = true;
}

/// <summary>
/// PostgreSQL function argument definition.
/// </summary>
public sealed record PostgresFunctionArgumentDefinition
{
    /// <summary>Argument name. Optional for inspected function identities.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>PostgreSQL argument type.</summary>
    public string Type { get; init; } = string.Empty;
}

/// <summary>
/// PostgreSQL grant definition for schema and table privileges.
/// Implements [RLS-PG-SUPPORT-DDL].
/// </summary>
public sealed record PostgresGrantDefinition
{
    /// <summary>Target schema.</summary>
    public string Schema { get; init; } = "public";

    /// <summary>Grant target kind.</summary>
    public PostgresGrantTarget Target { get; init; } = PostgresGrantTarget.Table;

    /// <summary>Table name when <see cref="Target" /> is <see cref="PostgresGrantTarget.Table" />.</summary>
    public string? ObjectName { get; init; }

    /// <summary>Privileges to grant, such as <c>USAGE</c>, <c>SELECT</c>, or <c>INSERT</c>.</summary>
    public IReadOnlyList<string> Privileges { get; init; } = [];

    /// <summary>Roles receiving the privileges.</summary>
    public IReadOnlyList<string> Roles { get; init; } = [];
}

/// <summary>
/// PostgreSQL grant target kind.
/// </summary>
public enum PostgresGrantTarget
{
    /// <summary>Target is a schema.</summary>
    Schema,

    /// <summary>Target is one table.</summary>
    Table,

    /// <summary>Target is every current table in the schema.</summary>
    AllTablesInSchema,
}

/// <summary>
/// Single table definition with columns, indexes, and all constraints.
/// </summary>
public sealed record TableDefinition
{
    /// <summary>Database schema (e.g., "public", "dbo").</summary>
    public string Schema { get; init; } = "public";

    /// <summary>Table name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Column definitions in order.</summary>
    public IReadOnlyList<ColumnDefinition> Columns { get; init; } = [];

    /// <summary>Index definitions.</summary>
    public IReadOnlyList<IndexDefinition> Indexes { get; init; } = [];

    /// <summary>Foreign key constraints.</summary>
    public IReadOnlyList<ForeignKeyDefinition> ForeignKeys { get; init; } = [];

    /// <summary>Primary key constraint.</summary>
    public PrimaryKeyDefinition? PrimaryKey { get; init; }

    /// <summary>Unique constraints (semantic alternative to unique indexes).</summary>
    public IReadOnlyList<UniqueConstraintDefinition> UniqueConstraints { get; init; } = [];

    /// <summary>Table-level check constraints (multi-column).</summary>
    public IReadOnlyList<CheckConstraintDefinition> CheckConstraints { get; init; } = [];

    /// <summary>Table comment/description for documentation.</summary>
    public string? Comment { get; init; }

    /// <summary>
    /// Row-level security policy set. When non-null, RLS is enabled on the
    /// table and each contained policy is applied. Implements [RLS-CORE-POLICY].
    /// </summary>
    public RlsPolicySetDefinition? RowLevelSecurity { get; init; }
}

/// <summary>
/// Column definition with type and all database-agnostic constraints.
/// </summary>
public sealed record ColumnDefinition
{
    /// <summary>Column name (case-insensitive for comparison).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Portable type with full precision/scale/length info.</summary>
    public required PortableType Type { get; init; }

    /// <summary>Whether NULL values are allowed.</summary>
    public bool IsNullable { get; init; } = true;

    /// <summary>SQL default expression (platform-specific, e.g., "CURRENT_TIMESTAMP").</summary>
    public string? DefaultValue { get; init; }

    /// <summary>
    /// LQL default expression (platform-independent, e.g., "now()", "gen_uuid()").
    /// When set, DDL generators translate this to platform-specific SQL.
    /// Takes precedence over DefaultValue if both are set.
    /// </summary>
    public string? DefaultLqlExpression { get; init; }

    /// <summary>Auto-increment/identity column.</summary>
    public bool IsIdentity { get; init; }

    /// <summary>Identity seed value (starting number).</summary>
    public long IdentitySeed { get; init; } = 1;

    /// <summary>Identity increment value.</summary>
    public long IdentityIncrement { get; init; } = 1;

    /// <summary>Computed column expression (if computed).</summary>
    public string? ComputedExpression { get; init; }

    /// <summary>Whether computed column is persisted/stored.</summary>
    public bool IsComputedPersisted { get; init; }

    /// <summary>Collation for string columns (e.g., "NOCASE", "en_US.UTF-8").</summary>
    public string? Collation { get; init; }

    /// <summary>Check constraint expression for this column only.</summary>
    public string? CheckConstraint { get; init; }

    /// <summary>Column comment/description for documentation.</summary>
    public string? Comment { get; init; }
}

/// <summary>
/// Check constraint that spans multiple columns.
/// </summary>
public sealed record CheckConstraintDefinition
{
    /// <summary>Constraint name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>SQL boolean expression (e.g., "Price >= 0 AND Quantity >= 0").</summary>
    public string Expression { get; init; } = string.Empty;
}

/// <summary>
/// Unique constraint (alternative to unique index for semantic clarity).
/// </summary>
public sealed record UniqueConstraintDefinition
{
    /// <summary>Constraint name.</summary>
    public string? Name { get; init; }

    /// <summary>Columns that must be unique together.</summary>
    public IReadOnlyList<string> Columns { get; init; } = [];
}

/// <summary>
/// Primary key constraint definition.
/// </summary>
public sealed record PrimaryKeyDefinition
{
    /// <summary>Constraint name.</summary>
    public string? Name { get; init; }

    /// <summary>Columns in the primary key.</summary>
    public IReadOnlyList<string> Columns { get; init; } = [];
}

/// <summary>
/// Index definition (unique or non-unique).
/// Supports both column-based indexes (Columns) and expression-based indexes (Expressions).
/// When Expressions is non-empty, it takes precedence over Columns.
/// </summary>
public sealed record IndexDefinition
{
    /// <summary>Index name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Columns in the index (quoted as identifiers).</summary>
    public IReadOnlyList<string> Columns { get; init; } = [];

    /// <summary>
    /// LQL/SQL expressions for expression-based indexes (e.g., "lower(name)").
    /// When non-empty, these are used instead of Columns and are emitted verbatim.
    /// </summary>
    public IReadOnlyList<string> Expressions { get; init; } = [];

    /// <summary>Whether the index enforces uniqueness.</summary>
    public bool IsUnique { get; init; }

    /// <summary>Partial index WHERE clause filter.</summary>
    public string? Filter { get; init; }
}

/// <summary>
/// Foreign key constraint definition.
/// </summary>
public sealed record ForeignKeyDefinition
{
    /// <summary>Constraint name.</summary>
    public string? Name { get; init; }

    /// <summary>Columns in the foreign key.</summary>
    public IReadOnlyList<string> Columns { get; init; } = [];

    /// <summary>Referenced table name.</summary>
    public string ReferencedTable { get; init; } = string.Empty;

    /// <summary>Referenced table schema.</summary>
    public string ReferencedSchema { get; init; } = "public";

    /// <summary>Referenced columns.</summary>
    public IReadOnlyList<string> ReferencedColumns { get; init; } = [];

    /// <summary>Action on DELETE.</summary>
    public ForeignKeyAction OnDelete { get; init; } = ForeignKeyAction.NoAction;

    /// <summary>Action on UPDATE.</summary>
    public ForeignKeyAction OnUpdate { get; init; } = ForeignKeyAction.NoAction;
}

/// <summary>
/// Foreign key referential action.
/// </summary>
public enum ForeignKeyAction
{
    /// <summary>No action on delete/update.</summary>
    NoAction,

    /// <summary>Cascade delete/update to child rows.</summary>
    Cascade,

    /// <summary>Set child column to NULL.</summary>
    SetNull,

    /// <summary>Set child column to default value.</summary>
    SetDefault,

    /// <summary>Prevent delete/update if children exist.</summary>
    Restrict,
}
