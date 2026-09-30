namespace Nimblesite.DataProvider.Migration.Tests;

/// <summary>One end-to-end regression suite for SQLite, PostgreSQL, and SQL Server.</summary>
[Collection(MigrationPlatformSuite.Name)]
public sealed partial record MigrationPlatformRegressionTests
{
    private readonly MigrationPostgresContainerFixture _postgres;
    private readonly SqlServerContainerFixture _sqlServer;

    public MigrationPlatformRegressionTests(
        MigrationPostgresContainerFixture postgres,
        SqlServerContainerFixture sqlServer
    )
    {
        _postgres = postgres;
        _sqlServer = sqlServer;
    }

    // Implements [MIG-DIFF-ADDITIVE]. Regression: #109.
    [Theory]
    [InlineData("sqlite", true, false)]
    [InlineData("postgres", true, false)]
    [InlineData("sqlserver", true, false)]
    [InlineData("sqlite", false, true)]
    [InlineData("postgres", false, true)]
    [InlineData("sqlserver", false, true)]
    [InlineData("sqlite", true, true)]
    [InlineData("postgres", true, true)]
    [InlineData("sqlserver", true, true)]
    public async Task AdditiveMigration_RelaxesNotNullWithoutDroppingOtherConstraints(
        string provider,
        bool stripeIdNullable,
        bool externalIdNullable
    )
    {
        await WithTargetAsync(
                provider,
                target => AssertNullableMigration(target, stripeIdNullable, externalIdNullable)
            )
            .ConfigureAwait(true);
    }

    private static void AssertNullableMigration(
        MigrationTarget target,
        bool stripeIdNullable,
        bool externalIdNullable
    )
    {
        var original = NullableSchema(stripeIdNullable: false, externalIdNullable: false);
        Migrate(target, original);
        AssertNullableColumns(target, stripeIdNullable: false, externalIdNullable: false);

        var desired = NullableSchema(stripeIdNullable, externalIdNullable);
        Migrate(target, desired);
        AssertNullableColumns(target, stripeIdNullable, externalIdNullable);
        Migrate(target, desired);
        AssertNullableColumns(target, stripeIdNullable, externalIdNullable);
    }

    private static void AssertNullableColumns(
        MigrationTarget target,
        bool stripeIdNullable,
        bool externalIdNullable
    )
    {
        AssertTableExists(target, "topup", expected: true);
        Assert.Equal(
            stripeIdNullable,
            ColumnIsNullable(target, "topup", "stripe_payment_intent_id")
        );
        Assert.Equal(externalIdNullable, ColumnIsNullable(target, "topup", "external_id"));
        Assert.False(ColumnIsNullable(target, "topup", "required_receipt_reference"));
        Assert.True(ColumnIsNullable(target, "topup", "optional_note"));
        Assert.False(ColumnIsNullable(target, "topup", "id"));
        Assert.False(ColumnIsNullable(target, "topup", "tenant_id"));
        AssertTableExists(target, "tenants", expected: true);
        Assert.False(ColumnIsNullable(target, "tenants", "id"));
        var foreignKey = Assert.Single(ReadForeignKeys(target, "topup"));
        Assert.Equal("tenant_id", foreignKey.Column);
        Assert.Equal("tenants", foreignKey.ReferencedTable);
        Assert.Equal("id", foreignKey.ReferencedColumn);
        Assert.Equal("CASCADE", NormalizedDeleteAction(foreignKey.OnDelete));
    }

    // Implements [MIG-DIFF-FOREIGN-KEY]. Regression: #79 and #80.
    [Theory]
    [InlineData("sqlite", false, false)]
    [InlineData("postgres", false, false)]
    [InlineData("sqlserver", false, false)]
    [InlineData("sqlite", true, false)]
    [InlineData("postgres", true, false)]
    [InlineData("sqlserver", true, false)]
    [InlineData("sqlite", false, true)]
    [InlineData("postgres", false, true)]
    [InlineData("sqlserver", false, true)]
    public async Task AdditiveMigration_AddsForeignKeyWhenParentOrColumnIsNew(
        string provider,
        bool parentInitiallyExists,
        bool columnInitiallyExists
    )
    {
        await WithTargetAsync(
                provider,
                target =>
                    AssertNewColumnForeignKeyMigration(
                        target,
                        parentInitiallyExists,
                        columnInitiallyExists
                    )
            )
            .ConfigureAwait(true);
    }

    private static void AssertNewColumnForeignKeyMigration(
        MigrationTarget target,
        bool parentInitiallyExists,
        bool columnInitiallyExists
    )
    {
        Migrate(
            target,
            ForeignKeySchema(
                parent: parentInitiallyExists,
                column: columnInitiallyExists,
                fk: false
            )
        );
        AssertTableExists(target, "usage_events", expected: true);
        AssertTableExists(target, "tenant_end_users", expected: parentInitiallyExists);
        AssertColumnExists(target, "usage_events", "end_user_id", expected: columnInitiallyExists);
        Assert.Empty(ReadForeignKeys(target, "usage_events"));

        var desired = ForeignKeySchema(parent: true, column: true, fk: true);
        Migrate(target, desired);
        AssertEndUserForeignKeyShape(target);
        Migrate(target, desired);
        AssertEndUserForeignKeyShape(target);
    }

    // Implements [MIG-DIFF-FOREIGN-KEY]. Regression: #79 and #80, including a rerun after the column was already added.
    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    public async Task AdditiveMigration_RepairsPartiallyMigratedForeignKey(string provider)
    {
        await WithTargetAsync(provider, AssertPartialForeignKeyMigration).ConfigureAwait(true);
    }

    private static void AssertPartialForeignKeyMigration(MigrationTarget target)
    {
        Migrate(target, ForeignKeySchema(parent: true, column: true, fk: false));
        AssertTableExists(target, "tenant_end_users", expected: true);
        AssertColumnExists(target, "usage_events", "end_user_id", expected: true);
        Assert.True(ColumnIsNullable(target, "usage_events", "end_user_id"));
        Assert.Empty(ReadForeignKeys(target, "usage_events"));

        var desired = ForeignKeySchema(parent: true, column: true, fk: true);
        Migrate(target, desired);
        AssertEndUserForeignKeyShape(target);
        Migrate(target, desired);
        AssertEndUserForeignKeyShape(target);
    }

    private static void AssertEndUserForeignKeyShape(MigrationTarget target)
    {
        AssertTableExists(target, "usage_events", expected: true);
        AssertTableExists(target, "tenant_end_users", expected: true);
        AssertColumnExists(target, "usage_events", "end_user_id", expected: true);
        Assert.True(ColumnIsNullable(target, "usage_events", "end_user_id"));
        Assert.False(ColumnIsNullable(target, "usage_events", "id"));
        Assert.False(ColumnIsNullable(target, "tenant_end_users", "id"));
        AssertEndUserForeignKey(target);
    }

    // Implements [MIG-DIFF-DESTRUCTIVE]. Regression: #105. Either obsolete FK may be removed.
    [Theory]
    [InlineData("sqlite", true, false)]
    [InlineData("postgres", true, false)]
    [InlineData("sqlserver", true, false)]
    [InlineData("sqlite", false, true)]
    [InlineData("postgres", false, true)]
    [InlineData("sqlserver", false, true)]
    public async Task DestructiveMigration_KeepsDeclaredForeignKeysAndRemovesOnlyObsoleteOne(
        string provider,
        bool keepTenantForeignKey,
        bool keepProjectForeignKey
    )
    {
        await WithTargetAsync(
                provider,
                target =>
                    AssertDestructiveForeignKeyMigration(
                        target,
                        keepTenantForeignKey,
                        keepProjectForeignKey
                    )
            )
            .ConfigureAwait(true);
    }

    private static void AssertDestructiveForeignKeyMigration(
        MigrationTarget target,
        bool keepTenantForeignKey,
        bool keepProjectForeignKey
    )
    {
        var original = ForeignKeyCleanupSchema(
            includeTenantForeignKey: true,
            includeProjectForeignKey: true,
            includeObsoleteTable: true
        );
        AssertRestoredCleanupSchema(target, original);
        AssertUndisturbedCleanupSchema(target, original);

        var desired = ForeignKeyCleanupSchema(
            includeTenantForeignKey: keepTenantForeignKey,
            includeProjectForeignKey: keepProjectForeignKey,
            includeObsoleteTable: false
        );
        AssertRemovedCleanupSchema(target, desired, keepTenantForeignKey, keepProjectForeignKey);
        AssertRemovedCleanupSchema(target, desired, keepTenantForeignKey, keepProjectForeignKey);
        AssertRestoredCleanupSchema(target, original);
        AssertRestoredCleanupSchema(target, original);
    }

    private static void AssertRemovedCleanupSchema(
        MigrationTarget target,
        SchemaDefinition desired,
        bool keepTenantForeignKey,
        bool keepProjectForeignKey
    ) =>
        MigrateAndAssertCleanup(
            target,
            desired,
            includeTenantForeignKey: keepTenantForeignKey,
            includeProjectForeignKey: keepProjectForeignKey,
            includeObsoleteTable: false,
            allowDestructive: true
        );

    private static void AssertRestoredCleanupSchema(
        MigrationTarget target,
        SchemaDefinition original
    ) =>
        MigrateAndAssertCleanup(
            target,
            original,
            includeTenantForeignKey: true,
            includeProjectForeignKey: true,
            includeObsoleteTable: true,
            allowDestructive: false
        );

    private static void AssertUndisturbedCleanupSchema(
        MigrationTarget target,
        SchemaDefinition original
    ) =>
        MigrateAndAssertCleanup(
            target,
            original,
            includeTenantForeignKey: true,
            includeProjectForeignKey: true,
            includeObsoleteTable: true,
            allowDestructive: true
        );

    private static void MigrateAndAssertCleanup(
        MigrationTarget target,
        SchemaDefinition schema,
        bool includeTenantForeignKey,
        bool includeProjectForeignKey,
        bool includeObsoleteTable,
        bool allowDestructive
    )
    {
        Migrate(target, schema, allowDestructive);
        AssertCleanupSchema(
            target,
            includeTenantForeignKey: includeTenantForeignKey,
            includeProjectForeignKey: includeProjectForeignKey
        );
        AssertTableExists(target, "obsolete_records", expected: includeObsoleteTable);
    }

    private static void AssertCleanupSchema(
        MigrationTarget target,
        bool includeTenantForeignKey,
        bool includeProjectForeignKey
    )
    {
        AssertCleanupForeignKeys(target, includeTenantForeignKey, includeProjectForeignKey);
        AssertTableExists(target, "tenants", expected: true);
        AssertTableExists(target, "projects", expected: true);
        AssertTableExists(target, "usage_events", expected: true);
        AssertTableExists(target, "audit_events", expected: true);
        AssertColumnExists(target, "usage_events", "project_id", expected: true);
        Assert.False(ColumnIsNullable(target, "usage_events", "project_id"));
        Assert.False(ColumnIsNullable(target, "usage_events", "tenant_id"));
        Assert.False(ColumnIsNullable(target, "audit_events", "tenant_id"));
    }

    // Implements [RLS-DIFF]. Regression: #98. Same-name USING and WITH CHECK changes are independent.
    [Theory]
    [InlineData("sqlite", false, true)]
    [InlineData("postgres", false, true)]
    [InlineData("sqlserver", false, true)]
    [InlineData("sqlite", true, false)]
    [InlineData("postgres", true, false)]
    [InlineData("sqlserver", true, false)]
    [InlineData("sqlite", false, false)]
    [InlineData("postgres", false, false)]
    [InlineData("sqlserver", false, false)]
    public async Task DestructiveMigration_ReplacesChangedPolicyPredicate(
        string provider,
        bool usingAllowed,
        bool checkAllowed
    )
    {
        await WithTargetAsync(
                provider,
                target => AssertPolicyMigration(target, usingAllowed, checkAllowed)
            )
            .ConfigureAwait(true);
    }

    private static void AssertPolicyMigration(
        MigrationTarget target,
        bool usingAllowed,
        bool checkAllowed
    )
    {
        var originalSchema = PolicySchema(usingAllowed: true, checkAllowed: true);
        Migrate(target, originalSchema);
        var original = ReadPolicy(target);
        Assert.False(string.IsNullOrWhiteSpace(original.Using));
        Assert.False(string.IsNullOrWhiteSpace(original.WithCheck));
        AssertPolicyMatches(target.Provider, original, usingAllowed: true, checkAllowed: true);

        var desired = PolicySchema(usingAllowed, checkAllowed);
        Migrate(target, desired, allowDestructive: true);
        var changed = ReadPolicy(target);
        AssertChangedClause(target.Provider, original, changed, usingAllowed, checkAllowed);
        AssertPolicyMatches(target.Provider, changed, usingAllowed, checkAllowed);
        Migrate(target, desired, allowDestructive: true);
        Assert.Equal(changed, ReadPolicy(target));
        AssertRestoredPolicy(target, originalSchema, changed, usingAllowed, checkAllowed);
    }

    private static void AssertRestoredPolicy(
        MigrationTarget target,
        SchemaDefinition originalSchema,
        PolicySnapshot changed,
        bool usingAllowed,
        bool checkAllowed
    )
    {
        Migrate(target, originalSchema, allowDestructive: true);
        var restored = ReadPolicy(target);
        AssertPolicyMatches(target.Provider, restored, usingAllowed: true, checkAllowed: true);
        Assert.Equal(usingAllowed, changed.Using == restored.Using);
        Assert.Equal(checkAllowed, changed.WithCheck == restored.WithCheck);
        Migrate(target, originalSchema, allowDestructive: true);
        Assert.Equal(restored, ReadPolicy(target));
    }
}
