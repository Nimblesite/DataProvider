namespace Nimblesite.DataProvider.Migration.Tests;

/// <summary>
/// Shared PostgreSQL E2E test helper: applies a desired schema via
/// inspect → diff → apply and asserts the migration succeeded.
/// </summary>
internal static class PostgresTestDb
{
    private const string SchemaName = "public";

    public static void ApplySchema(
        NpgsqlConnection connection,
        SchemaDefinition desired,
        ILogger logger,
        bool allowDestructive = false
    )
    {
        var current = Assert
            .IsType<SchemaResultOk>(PostgresSchemaInspector.Inspect(connection, "public", logger))
            .Value;
        var ops = Assert
            .IsType<OperationsResultOk>(
                SchemaDiff.Calculate(current, desired, allowDestructive, logger: logger)
            )
            .Value;

        var apply = MigrationRunner.Apply(
            connection,
            ops,
            PostgresDdlGenerator.Generate,
            allowDestructive ? MigrationOptions.Destructive : MigrationOptions.Default,
            logger
        );
        var error = apply is MigrationApplyResultError failure ? failure.Value.Message : null;
        Assert.True(apply is MigrationApplyResultOk, $"Migration failed: {error}");
    }

    /// <summary>Executes a non-query SQL statement on the connection.</summary>
    internal static void Exec(NpgsqlConnection conn, string sql)
    {
        using var command = conn.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    /// <summary>Executes a non-query SQL statement within the given transaction.</summary>
    internal static void Exec(NpgsqlConnection conn, NpgsqlTransaction tx, string sql)
    {
        using var command = conn.CreateCommand();
        command.Transaction = tx;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    /// <summary>Inspects the live PostgreSQL schema, asserting success.</summary>
    internal static SchemaDefinition Inspect(NpgsqlConnection connection, ILogger logger)
    {
        var result = PostgresSchemaInspector.Inspect(connection, SchemaName, logger);
        if (result is SchemaResultOk ok)
        {
            return ok.Value;
        }

        Assert.Fail("Expected PostgreSQL schema inspection to succeed.");
        return new SchemaDefinition { Name = "failed", Tables = [] };
    }

    /// <summary>Calculates the schema diff between current and desired, asserting success.</summary>
    internal static IReadOnlyList<SchemaOperation> Calculate(
        SchemaDefinition current,
        SchemaDefinition desired,
        ILogger logger,
        bool allowDestructive = false
    )
    {
        var result = SchemaDiff.Calculate(current, desired, allowDestructive, logger);
        if (result is OperationsResultOk ok)
        {
            return ok.Value;
        }

        Assert.Fail("Expected PostgreSQL schema diff to succeed.");
        return [];
    }

    /// <summary>Applies the operations against PostgreSQL, asserting the migration succeeded.</summary>
    internal static void Apply(
        NpgsqlConnection connection,
        IReadOnlyList<SchemaOperation> operations,
        ILogger logger,
        MigrationOptions? options = null
    )
    {
        var result = MigrationRunner.Apply(
            connection,
            operations,
            PostgresDdlGenerator.Generate,
            options ?? MigrationOptions.Default,
            logger
        );
        var failure = result is MigrationApplyResultError error ? error.Value.ToString() : "";

        Assert.True(result is MigrationApplyResultOk, $"Migration failed: {failure}");
    }
}
