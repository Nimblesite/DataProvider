namespace Nimblesite.DataProvider.Migration.Tests;

[Collection(PostgresTestSuite.Name)]
public sealed partial class PostgresDropConstraintBackedIndexTests(PostgresContainerFixture fixture)
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
            PostgresTestDb.ApplySchema(connection, SchemaWithUniqueConstraint(), Logger);
            var upgrade = PostgresTestDb.Calculate(
                PostgresTestDb.Inspect(connection, Logger),
                SchemaWithoutUniqueConstraint(),
                Logger,
                allowDestructive: true
            );

            Assert.Contains(upgrade, IsConstraintBackedIndexDrop);

            PostgresTestDb.Apply(connection, upgrade, Logger, MigrationOptions.Destructive);

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
            var desired = VerifyReusableWorkflow30(
                input0: SchemaWithUniqueConstraint,
                input1: connection,
                input2: Logger
            );

            var inspected = PostgresTestDb
                .Inspect(connection, Logger)
                .Tables.Single(t => t.Name == TableName);
            var converged = PostgresTestDb.Calculate(
                PostgresTestDb.Inspect(connection, Logger),
                desired,
                Logger,
                allowDestructive: true
            );

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

            PostgresTestDb.Apply(connection, converged, Logger, MigrationOptions.Destructive);

            Assert.True(ConstraintExists(connection));
            Assert.True(IndexExists(connection));
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(true);
            NpgsqlConnection.ClearPool(connection);
        }
    }

    private static bool IsConstraintBackedIndexDrop(SchemaOperation operation) =>
        operation
            is DropIndexOperation
            {
                Schema: SchemaName,
                TableName: TableName,
                IndexName: ConstraintName,
            };

    private static bool ConstraintExists(NpgsqlConnection connection) =>
        ObjectExists(connection, Data_ConstraintExists_1);

    private static bool IndexExists(NpgsqlConnection connection) =>
        ObjectExists(connection, Data_IndexExists_1);

    private static bool ObjectExists(NpgsqlConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddObjectParameters(command);
        return command.ExecuteScalar() is bool exists && exists;
    }

    private static void AddObjectParameters(NpgsqlCommand command)
    {
        command.Parameters.AddWithValue("@schema", SchemaName);
        command.Parameters.AddWithValue("@table", TableName);
        command.Parameters.AddWithValue("@constraint", ConstraintName);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static SchemaDefinition VerifyReusableWorkflow30(
        Func<SchemaDefinition> input0,
        NpgsqlConnection input1,
        ILogger input2
    )
    {
        var desired = input0();

        PostgresTestDb.Apply(
            input1,
            PostgresTestDb.Calculate(PostgresTestDb.Inspect(input1, input2), desired, input2),
            input2
        );

        return desired;
    }
}
