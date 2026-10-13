// Implements [MIG-TEST-SCHEMA-FIXTURES].
using System.Collections.Immutable;

namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [MIG-TEST-DIFF-SHARED].

// Implements [MIG-EXISTING-DATABASE-UPGRADE] and [RLS-DIFF].
[Collection(PostgresTestSuite.Name)]
public sealed partial class PostgresUpgradeBlockersTests(PostgresContainerFixture fixture)
{
    [Fact]
    public void AdditiveUpgrade_RelaxesNotNullOnExistingTableAndRerunsCleanly()
    {
        using var connection = fixture.CreateDatabase("nullable_upgrade");
        Migrate(connection, NullableSchema(nullable: false));
        var desired = NullableSchema(nullable: true);

        Migrate(connection, desired);

        var notes = Inspect(connection).Tables.Single().Columns.Single(c => c.Name == "notes");
        Assert.True(notes.IsNullable);
        AssertNoDrift(connection, desired);
        Assert.Empty(Migrate(connection, desired));
    }

    [Fact]
    public void AdditiveUpgrade_AddsUnnamedForeignKeyAlongsideNewColumn()
    {
        using var connection = fixture.CreateDatabase("foreign_key_upgrade");
        Migrate(connection, ForeignKeySchema(hasReferenceColumn: false));
        var desired = ForeignKeySchema(hasReferenceColumn: true);

        AssertForeignKeyConverged(connection, desired);
    }

    [Fact]
    public void AdditiveUpgrade_RepairsMissingForeignKeyAfterColumnWasApplied()
    {
        using var connection = fixture.CreateDatabase("partial_foreign_key_upgrade");
        Migrate(connection, ForeignKeySchema(hasReferenceColumn: false));
        var desired = ForeignKeySchema(hasReferenceColumn: true);
        var addedColumn = Assert.Single(
            SchemaDiffAssertions
                .Diff(current: Inspect(connection), desired: desired)
                .OfType<AddColumnOperation>()
        );
        Apply(connection, [addedColumn]);

        AssertForeignKeyConverged(connection, desired);
    }

    [Fact]
    public void AdditiveUpgrade_ReplacesChangedUsingAndWithCheckPolicyPredicates()
    {
        using var connection = fixture.CreateDatabase("policy_upgrade");
        Migrate(connection, PolicySchema("IS NOT NULL"));
        var desired = PolicySchema("IS NULL");
        var upgrade = SchemaDiffAssertions.Diff(current: Inspect(connection), desired: desired);

        Assert.NotEmpty(upgrade);
        Assert.Contains(VerifyMismatches(connection, desired), m => m.Contains("policy"));
        Apply(connection, upgrade);

        var policy = Inspect(connection).Tables.Single().RowLevelSecurity?.Policies.Single();
        Assert.Contains("IS NULL", policy?.UsingSql);
        Assert.Contains("IS NULL", policy?.WithCheckSql);
        AssertConverged(connection, desired);
    }

    [Fact]
    public void DestructiveCleanup_DropsOnlyObsoleteForeignKeyAndRerunsCleanly()
    {
        using var connection = fixture.CreateDatabase("foreign_key_cleanup");
        Migrate(connection, CleanupSchema(includeObsolete: true));
        var desired = CleanupSchema(includeObsolete: false);
        var cleanup = SchemaDiffAssertions.Diff(
            current: Inspect(connection),
            desired: desired,
            destructive: true
        );

        var drop = Assert.Single(cleanup.OfType<DropForeignKeyOperation>());
        Assert.Equal("FK_children_obsolete_id", drop.ConstraintName);
        Apply(connection, cleanup, destructive: true);

        var foreignKeys = Inspect(connection).Tables.Single(t => t.Name == "children").ForeignKeys;
        Assert.Equal(2, foreignKeys.Count);
        Assert.Contains(foreignKeys, fk => fk.Columns.Single() == "kept_a_id");
        Assert.Contains(foreignKeys, fk => fk.Columns.Single() == "kept_b_id");
        AssertConverged(connection, desired, destructive: true);
    }

    private static SchemaDefinition CleanupSchema(bool includeObsolete)
    {
        ForeignKeyDefinition[] kept =
        [
            ForeignKey("kept_a_id", "kept_a"),
            ForeignKey("kept_b_id", "kept_b") with
            {
                Name = "FK_children_kept_b_id",
            },
        ];
        var foreignKeys = includeObsolete
            ? kept.Append(
                    ForeignKey("obsolete_id", "obsolete") with
                    {
                        Name = "FK_children_obsolete_id",
                    }
                )
                .ToArray()
            : kept;
        return SchemaTestData.Schema(
            name: "foreign_key_cleanup",
            tables:
            [
                Parent("kept_a"),
                Parent("kept_b"),
                Parent("obsolete"),
                SchemaTestData.Table(
                    name: "children",
                    columns:
                    [
                        Id(),
                        UuidColumn("kept_a_id"),
                        UuidColumn("kept_b_id"),
                        UuidColumn("obsolete_id"),
                    ],
                    primaryKey: new PrimaryKeyDefinition { Columns = ["id"] },
                    foreignKeys: foreignKeys
                ),
            ]
        );
    }

    private static ColumnDefinition Id() =>
        SchemaTestData.Column(name: "id", type: PortableTypes.Uuid, isNullable: false);

    private static ColumnDefinition UuidColumn(string name) =>
        SchemaTestData.Column(name: name, type: PortableTypes.Uuid);

    private static SchemaDefinition Inspect(NpgsqlConnection connection) =>
        global::Tests
            .Shared.ResultAssertions.ExactOk(
                actual: PostgresSchemaInspector.Inspect(connection, "public", NullLogger.Instance)
            )
            .Value;

    private static IReadOnlyList<SchemaOperation> Migrate(
        NpgsqlConnection connection,
        SchemaDefinition desired,
        bool destructive = false
    )
    {
        var operations = SchemaDiffAssertions.Diff(
            current: Inspect(connection),
            desired: desired,
            destructive: destructive
        );
        Apply(connection, operations, destructive);
        return operations;
    }

    private static void Apply(
        NpgsqlConnection connection,
        IReadOnlyList<SchemaOperation> operations,
        bool destructive = false
    )
    {
        var result = MigrationRunner.Apply(
            connection,
            operations,
            PostgresDdlGenerator.Generate,
            destructive ? MigrationOptions.Destructive : MigrationOptions.Default
        );
        SqliteTestDb.AssertApplyOk(result: result);
    }

    private static void AssertForeignKeyConverged(
        NpgsqlConnection connection,
        SchemaDefinition desired
    )
    {
        Migrate(connection, desired);
        Assert.Single(Inspect(connection).Tables.Single(t => t.Name == "children").ForeignKeys);
        AssertConverged(connection, desired);
    }

    private static void AssertConverged(
        NpgsqlConnection connection,
        SchemaDefinition desired,
        bool destructive = false
    )
    {
        AssertNoDrift(connection, desired);
        Assert.Empty(Migrate(connection, desired, destructive));
    }

    private static ImmutableArray<string> VerifyMismatches(
        NpgsqlConnection connection,
        SchemaDefinition desired
    ) =>
        Assert
            .IsType<SchemaIntegrityOk>(SchemaIntegrityVerifier.Verify(Inspect(connection), desired))
            .Value;

    private static void AssertNoDrift(NpgsqlConnection connection, SchemaDefinition desired) =>
        Assert.Empty(VerifyMismatches(connection, desired));
}
