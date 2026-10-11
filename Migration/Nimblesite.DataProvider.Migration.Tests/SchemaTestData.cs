namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [MIG-TEST-SCHEMA-FIXTURES].
internal static class SchemaTestData
{
    internal static CheckConstraintDefinition CheckConstraint(
        string name = "",
        string expression = ""
    ) => new() { Name = name, Expression = expression };

    internal static ColumnDefinition Column(
        PortableType type,
        string name = "",
        bool isNullable = true,
        string? defaultValue = null,
        string? defaultLqlExpression = null,
        bool isIdentity = false,
        long identitySeed = 1,
        long identityIncrement = 1,
        string? computedExpression = null,
        bool isComputedPersisted = false,
        string? collation = null,
        string? checkConstraint = null,
        string? checkConstraintName = null,
        string? comment = null
    ) =>
        new()
        {
            Type = type,
            Name = name,
            IsNullable = isNullable,
            DefaultValue = defaultValue,
            DefaultLqlExpression = defaultLqlExpression,
            IsIdentity = isIdentity,
            IdentitySeed = identitySeed,
            IdentityIncrement = identityIncrement,
            ComputedExpression = computedExpression,
            IsComputedPersisted = isComputedPersisted,
            Collation = collation,
            CheckConstraint = checkConstraint,
            CheckConstraintName = checkConstraintName,
            Comment = comment,
        };

    internal static ForeignKeyDefinition ForeignKey(
        string? name = null,
        IReadOnlyList<string>? columns = null,
        string referencedTable = "",
        string referencedSchema = "public",
        IReadOnlyList<string>? referencedColumns = null,
        ForeignKeyAction onDelete = ForeignKeyAction.NoAction,
        ForeignKeyAction onUpdate = ForeignKeyAction.NoAction
    ) =>
        new()
        {
            Name = name,
            Columns = columns ?? [],
            ReferencedTable = referencedTable,
            ReferencedSchema = referencedSchema,
            ReferencedColumns = referencedColumns ?? [],
            OnDelete = onDelete,
            OnUpdate = onUpdate,
        };

    internal static IndexDefinition Index(
        string name = "",
        IReadOnlyList<string>? columns = null,
        IReadOnlyList<string>? expressions = null,
        bool isUnique = false,
        string? filter = null
    ) =>
        new()
        {
            Name = name,
            Columns = columns ?? [],
            Expressions = expressions ?? [],
            IsUnique = isUnique,
            Filter = filter,
        };

    internal static PostgresFunctionArgumentDefinition PostgresFunctionArgument(
        string name = "",
        string type = ""
    ) => new() { Name = name, Type = type };

    internal static PostgresFunctionDefinition PostgresFunction(
        string schema = "public",
        string name = "",
        IReadOnlyList<PostgresFunctionArgumentDefinition>? arguments = null,
        string returns = "void",
        string language = "sql",
        string volatility = "stable",
        bool securityDefiner = false,
        string body = "",
        string? bodyLql = null,
        IReadOnlyList<string>? executeRoles = null,
        bool revokePublicExecute = true
    ) =>
        new()
        {
            Schema = schema,
            Name = name,
            Arguments = arguments ?? [],
            Returns = returns,
            Language = language,
            Volatility = volatility,
            SecurityDefiner = securityDefiner,
            Body = body,
            BodyLql = bodyLql,
            ExecuteRoles = executeRoles ?? [],
            RevokePublicExecute = revokePublicExecute,
        };

    internal static PostgresGrantDefinition PostgresGrant(
        string schema = "public",
        PostgresGrantTarget target = PostgresGrantTarget.Table,
        string? objectName = null,
        IReadOnlyList<string>? privileges = null,
        IReadOnlyList<string>? roles = null,
        string? runAs = null
    ) =>
        new()
        {
            Schema = schema,
            Target = target,
            ObjectName = objectName,
            Privileges = privileges ?? [],
            Roles = roles ?? [],
            RunAs = runAs,
        };

    internal static PostgresRoleDefinition PostgresRole(
        string name = "",
        bool login = false,
        bool bypassRls = false,
        IReadOnlyList<string>? grantTo = null
    ) =>
        new()
        {
            Name = name,
            Login = login,
            BypassRls = bypassRls,
            GrantTo = grantTo ?? [],
        };

    internal static PrimaryKeyDefinition PrimaryKey(
        string? name = null,
        IReadOnlyList<string>? columns = null
    ) => new() { Name = name, Columns = columns ?? [] };

    internal static RlsPolicyDefinition RlsPolicy(
        string name = "",
        bool isPermissive = true,
        IReadOnlyList<RlsOperation> operations = [RlsOperation.All],
        IReadOnlyList<string>? roles = null,
        string? usingLql = null,
        string? withCheckLql = null,
        string? usingSql = null,
        string? withCheckSql = null
    ) =>
        new()
        {
            Name = name,
            IsPermissive = isPermissive,
            Operations = operations,
            Roles = roles ?? [],
            UsingLql = usingLql,
            WithCheckLql = withCheckLql,
            UsingSql = usingSql,
            WithCheckSql = withCheckSql,
        };

    internal static RlsPolicySetDefinition RlsPolicySet(
        bool enabled = true,
        IReadOnlyList<RlsPolicyDefinition>? policies = null,
        bool forced = false
    ) =>
        new()
        {
            Enabled = enabled,
            Policies = policies ?? [],
            Forced = forced,
        };

    internal static SchemaDefinition Schema(
        string name = "",
        IReadOnlyList<TableDefinition>? tables = null,
        IReadOnlyList<PostgresRoleDefinition>? roles = null,
        IReadOnlyList<PostgresFunctionDefinition>? functions = null,
        IReadOnlyList<PostgresGrantDefinition>? grants = null
    ) =>
        new()
        {
            Name = name,
            Tables = tables ?? [],
            Roles = roles ?? [],
            Functions = functions ?? [],
            Grants = grants ?? [],
        };

    internal static TableDefinition Table(
        string schema = "public",
        string name = "",
        IReadOnlyList<ColumnDefinition>? columns = null,
        IReadOnlyList<IndexDefinition>? indexes = null,
        IReadOnlyList<ForeignKeyDefinition>? foreignKeys = null,
        PrimaryKeyDefinition? primaryKey = null,
        IReadOnlyList<UniqueConstraintDefinition>? uniqueConstraints = null,
        IReadOnlyList<CheckConstraintDefinition>? checkConstraints = null,
        string? comment = null,
        RlsPolicySetDefinition? rowLevelSecurity = null,
        IReadOnlyList<TriggerDefinition>? triggers = null
    ) =>
        new()
        {
            Schema = schema,
            Name = name,
            Columns = columns ?? [],
            Indexes = indexes ?? [],
            ForeignKeys = foreignKeys ?? [],
            PrimaryKey = primaryKey,
            UniqueConstraints = uniqueConstraints ?? [],
            CheckConstraints = checkConstraints ?? [],
            Comment = comment,
            RowLevelSecurity = rowLevelSecurity,
            Triggers = triggers ?? [],
        };

    internal static UniqueConstraintDefinition UniqueConstraint(
        string? name = null,
        IReadOnlyList<string>? columns = null
    ) => new() { Name = name, Columns = columns ?? [] };
}
