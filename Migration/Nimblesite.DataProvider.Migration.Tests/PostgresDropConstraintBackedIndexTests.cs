namespace Nimblesite.DataProvider.Migration.Tests;

[Collection(PostgresTestSuite.Name)]
public sealed class PostgresDropConstraintBackedIndexTests(PostgresContainerFixture fixture)
{
    private const string SchemaName = "public";
    private const string TableName = "api_keys";
    private const string ConstraintName = "api_keys_key_hash_uniq";
    private static readonly ILogger Logger = NullLogger.Instance;

    [Fact]
    public async Task DropIndex_WhenIndexBacksUniqueConstraint_DropsConstraint()
    {
        // Implements [MIG-PG-CONSTRAINT-BACKED-INDEX-DROP].
        var connection = await fixture
            .CreateDatabaseAsync("drop_constraint_index")
            .ConfigureAwait(true);

        try
        {
            Apply(connection, Calculate(Inspect(connection), SchemaWithUniqueConstraint()));
            var upgrade = Calculate(Inspect(connection), SchemaWithoutUniqueConstraint(), true);

            Assert.Contains(upgrade, IsConstraintBackedIndexDrop);

            Apply(connection, upgrade, MigrationOptions.Destructive);

            Assert.False(ConstraintExists(connection));
            Assert.False(IndexExists(connection));
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(true);
            NpgsqlConnection.ClearPool(connection);
        }
    }

    [Fact]
    public async Task Calculate_WhenUniqueConstraintSchemaConverged_DoesNotDropBackingIndex()
    {
        // Implements [MIG-PG-UNIQUE-CONSTRAINT-INSPECTION].
        var connection = await fixture
            .CreateDatabaseAsync("converged_unique_constraint")
            .ConfigureAwait(true);

        try
        {
            var desired = SchemaWithUniqueConstraint();

            Apply(connection, Calculate(Inspect(connection), desired));

            var inspected = Inspect(connection).Tables.Single(t => t.Name == TableName);
            var converged = Calculate(Inspect(connection), desired, true);

            Assert.Contains(
                inspected.UniqueConstraints,
                constraint =>
                    constraint.Name == ConstraintName
                    && constraint.Columns.SequenceEqual(["key_hash"])
            );
            Assert.DoesNotContain(converged, IsConstraintBackedIndexDrop);
            Assert.DoesNotContain(
                converged,
                operation => operation is AddUniqueConstraintOperation
            );

            Apply(connection, converged, MigrationOptions.Destructive);

            Assert.True(ConstraintExists(connection));
            Assert.True(IndexExists(connection));
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(true);
            NpgsqlConnection.ClearPool(connection);
        }
    }

    private static SchemaDefinition SchemaWithUniqueConstraint() =>
        Schema
            .Define("Issue49")
            .Table(
                SchemaName,
                TableName,
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("key_hash", PortableTypes.VarChar(255), c => c.NotNull())
                        .Unique(ConstraintName, "key_hash")
            )
            .Build();

    private static SchemaDefinition SchemaWithoutUniqueConstraint() =>
        Schema
            .Define("Issue49")
            .Table(
                SchemaName,
                TableName,
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("key_hash", PortableTypes.VarChar(255), c => c.NotNull())
            )
            .Build();

    private static SchemaDefinition Inspect(NpgsqlConnection connection)
    {
        var result = PostgresSchemaInspector.Inspect(connection, SchemaName, Logger);
        if (result is SchemaResultOk ok)
        {
            return ok.Value;
        }

        Assert.Fail("Expected PostgreSQL schema inspection to succeed.");
        return Schema.Define("failed").Build();
    }

    private static IReadOnlyList<SchemaOperation> Calculate(
        SchemaDefinition current,
        SchemaDefinition desired,
        bool allowDestructive = false
    )
    {
        var result = SchemaDiff.Calculate(current, desired, allowDestructive, Logger);
        if (result is OperationsResultOk ok)
        {
            return ok.Value;
        }

        Assert.Fail("Expected PostgreSQL schema diff to succeed.");
        return [];
    }

    private static void Apply(
        NpgsqlConnection connection,
        IReadOnlyList<SchemaOperation> operations,
        MigrationOptions? options = null
    )
    {
        var result = MigrationRunner.Apply(
            connection,
            operations,
            PostgresDdlGenerator.Generate,
            options ?? MigrationOptions.Default,
            Logger
        );
        var failure = result is MigrationApplyResultError error ? error.Value.ToString() : "";

        Assert.True(result is MigrationApplyResultOk, $"Migration failed: {failure}");
    }

    private static bool IsConstraintBackedIndexDrop(SchemaOperation operation) =>
        operation
            is DropIndexOperation
            {
                Schema: SchemaName,
                TableName: TableName,
                IndexName: ConstraintName,
            };

    private static bool ConstraintExists(NpgsqlConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS (
                SELECT 1
                FROM pg_constraint c
                JOIN pg_class t ON t.oid = c.conrelid
                JOIN pg_namespace n ON n.oid = t.relnamespace
                WHERE n.nspname = @schema
                AND t.relname = @table
                AND c.conname = @constraint
            )
            """;
        AddObjectParameters(command);
        return command.ExecuteScalar() is bool exists && exists;
    }

    private static bool IndexExists(NpgsqlConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS (
                SELECT 1
                FROM pg_class i
                JOIN pg_namespace n ON n.oid = i.relnamespace
                WHERE n.nspname = @schema
                AND i.relname = @constraint
                AND i.relkind = 'i'
            )
            """;
        AddObjectParameters(command);
        return command.ExecuteScalar() is bool exists && exists;
    }

    private static void AddObjectParameters(NpgsqlCommand command)
    {
        command.Parameters.AddWithValue("@schema", SchemaName);
        command.Parameters.AddWithValue("@table", TableName);
        command.Parameters.AddWithValue("@constraint", ConstraintName);
    }
}
