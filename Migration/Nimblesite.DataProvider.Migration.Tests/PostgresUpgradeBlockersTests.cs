using System.Collections.Immutable;
using SchemaIntegrityOk = Outcome.Result<
    System.Collections.Immutable.ImmutableArray<string>,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Ok<
    System.Collections.Immutable.ImmutableArray<string>,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>;

namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [MIG-EXISTING-DATABASE-UPGRADE] and [RLS-DIFF].
[Collection(PostgresTestSuite.Name)]
public sealed class PostgresUpgradeBlockersTests(PostgresContainerFixture fixture)
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

        Migrate(connection, desired);

        Assert.Single(Inspect(connection).Tables.Single(t => t.Name == "children").ForeignKeys);
        AssertNoDrift(connection, desired);
        Assert.Empty(Migrate(connection, desired));
    }

    [Fact]
    public void AdditiveUpgrade_RepairsMissingForeignKeyAfterColumnWasApplied()
    {
        using var connection = fixture.CreateDatabase("partial_foreign_key_upgrade");
        Migrate(connection, ForeignKeySchema(hasReferenceColumn: false));
        var desired = ForeignKeySchema(hasReferenceColumn: true);
        var addedColumn = Assert.Single(
            Diff(Inspect(connection), desired).OfType<AddColumnOperation>()
        );
        Apply(connection, [addedColumn]);

        Migrate(connection, desired);

        Assert.Single(Inspect(connection).Tables.Single(t => t.Name == "children").ForeignKeys);
        AssertNoDrift(connection, desired);
        Assert.Empty(Migrate(connection, desired));
    }

    [Fact]
    public void AdditiveUpgrade_ReplacesChangedUsingAndWithCheckPolicyPredicates()
    {
        using var connection = fixture.CreateDatabase("policy_upgrade");
        Migrate(connection, PolicySchema("IS NOT NULL"));
        var desired = PolicySchema("IS NULL");
        var upgrade = Diff(Inspect(connection), desired);

        Assert.NotEmpty(upgrade);
        Assert.Contains(VerifyMismatches(connection, desired), m => m.Contains("policy"));
        Apply(connection, upgrade);

        var policy = Inspect(connection).Tables.Single().RowLevelSecurity?.Policies.Single();
        Assert.Contains("IS NULL", policy?.UsingSql);
        Assert.Contains("IS NULL", policy?.WithCheckSql);
        AssertNoDrift(connection, desired);
        Assert.Empty(Migrate(connection, desired));
    }

    [Fact]
    public void DestructiveCleanup_DropsOnlyObsoleteForeignKeyAndRerunsCleanly()
    {
        using var connection = fixture.CreateDatabase("foreign_key_cleanup");
        Migrate(connection, CleanupSchema(includeObsolete: true));
        var desired = CleanupSchema(includeObsolete: false);
        var cleanup = Diff(Inspect(connection), desired, destructive: true);

        var drop = Assert.Single(cleanup.OfType<DropForeignKeyOperation>());
        Assert.Equal("FK_children_obsolete_id", drop.ConstraintName);
        Apply(connection, cleanup, destructive: true);

        var foreignKeys = Inspect(connection).Tables.Single(t => t.Name == "children").ForeignKeys;
        Assert.Equal(2, foreignKeys.Count);
        Assert.Contains(foreignKeys, fk => fk.Columns.Single() == "kept_a_id");
        Assert.Contains(foreignKeys, fk => fk.Columns.Single() == "kept_b_id");
        AssertNoDrift(connection, desired);
        Assert.Empty(Migrate(connection, desired, destructive: true));
    }

    private static SchemaDefinition NullableSchema(bool nullable) =>
        new()
        {
            Name = "nullable_upgrade",
            Tables =
            [
                new TableDefinition
                {
                    Name = "documents",
                    Columns =
                    [
                        Id(),
                        new ColumnDefinition
                        {
                            Name = "notes",
                            Type = PortableTypes.Text,
                            IsNullable = nullable,
                        },
                    ],
                    PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
                },
            ],
        };

    private static SchemaDefinition ForeignKeySchema(bool hasReferenceColumn) =>
        new()
        {
            Name = "foreign_key_upgrade",
            Tables =
            [
                Parent("parents"),
                new TableDefinition
                {
                    Name = "children",
                    Columns = hasReferenceColumn ? [Id(), UuidColumn("parent_id")] : [Id()],
                    PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
                    ForeignKeys = hasReferenceColumn ? [ForeignKey("parent_id", "parents")] : [],
                },
            ],
        };

    private static SchemaDefinition PolicySchema(string predicate) =>
        new()
        {
            Name = "policy_upgrade",
            Tables =
            [
                new TableDefinition
                {
                    Name = "documents",
                    Columns = [Id(), UuidColumn("owner_id")],
                    PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
                    RowLevelSecurity = new RlsPolicySetDefinition
                    {
                        Policies =
                        [
                            new RlsPolicyDefinition
                            {
                                Name = "owner_policy",
                                UsingSql = $"owner_id {predicate}",
                                WithCheckSql = $"owner_id {predicate}",
                            },
                        ],
                    },
                },
            ],
        };

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
        return new SchemaDefinition
        {
            Name = "foreign_key_cleanup",
            Tables =
            [
                Parent("kept_a"),
                Parent("kept_b"),
                Parent("obsolete"),
                new TableDefinition
                {
                    Name = "children",
                    Columns =
                    [
                        Id(),
                        UuidColumn("kept_a_id"),
                        UuidColumn("kept_b_id"),
                        UuidColumn("obsolete_id"),
                    ],
                    PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
                    ForeignKeys = foreignKeys,
                },
            ],
        };
    }

    private static TableDefinition Parent(string name) =>
        new()
        {
            Name = name,
            Columns = [Id()],
            PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
        };

    private static ColumnDefinition Id() =>
        new()
        {
            Name = "id",
            Type = PortableTypes.Uuid,
            IsNullable = false,
        };

    private static ColumnDefinition UuidColumn(string name) =>
        new() { Name = name, Type = PortableTypes.Uuid };

    private static ForeignKeyDefinition ForeignKey(string column, string table) =>
        new()
        {
            Columns = [column],
            ReferencedTable = table,
            ReferencedColumns = ["id"],
        };

    private static SchemaDefinition Inspect(NpgsqlConnection connection) =>
        Assert
            .IsType<SchemaResultOk>(
                PostgresSchemaInspector.Inspect(connection, "public", NullLogger.Instance)
            )
            .Value;

    private static IReadOnlyList<SchemaOperation> Diff(
        SchemaDefinition current,
        SchemaDefinition desired,
        bool destructive = false
    ) =>
        Assert
            .IsType<OperationsResultOk>(
                SchemaDiff.Calculate(current, desired, allowDestructive: destructive)
            )
            .Value;

    private static IReadOnlyList<SchemaOperation> Migrate(
        NpgsqlConnection connection,
        SchemaDefinition desired,
        bool destructive = false
    )
    {
        var operations = Diff(Inspect(connection), desired, destructive);
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
        var failure = result is MigrationApplyResultError error ? error.Value.ToString() : "";
        Assert.True(result is MigrationApplyResultOk, $"Migration failed: {failure}");
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
