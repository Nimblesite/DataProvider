namespace Nimblesite.DataProvider.Migration.Tests;

/// <summary>
/// Implements [MIG-CHECK-CONSTRAINT-EXPRESSION-DRIFT] (#57): when a YAML
/// CHECK constraint changes its predicate but keeps the same name, the
/// migrator must drop the live constraint and add the desired one. Closes
/// the silent prod outage where a renamed enum-style CHECK was skipped
/// because the diff matched on name only.
/// </summary>
[Collection(PostgresTestSuite.Name)]
public sealed class PostgresCheckConstraintReplacementIssue57Tests(PostgresContainerFixture fixture)
{
    private const string SchemaName = "public";
    private static readonly ILogger Logger = NullLogger.Instance;

    [Fact]
    public async Task Calculate_TableCheckExpressionChanged_RegeneratesConstraintInPostgres()
    {
        var connection = await fixture.CreateDatabaseAsync("issue57_check").ConfigureAwait(true);

        try
        {
            PostgresTestDb.Apply(
                connection,
                PostgresTestDb.Calculate(
                    PostgresTestDb.Inspect(connection, Logger),
                    SchemaWithKindCheck("usage_events_kind_check_v1"),
                    Logger
                ),
                Logger
            );
            Assert.True(LiveExpressionContains(connection, "input_tokens"));
            Assert.False(LiveExpressionContains(connection, "llm_input_tokens"));

            var upgrade = PostgresTestDb.Calculate(
                PostgresTestDb.Inspect(connection, Logger),
                SchemaWithKindCheck("usage_events_kind_check_v2"),
                Logger
            );
            Assert.Contains(
                upgrade,
                op =>
                    op is DropCheckConstraintOperation drop
                    && drop.ConstraintName == "usage_events_kind_check"
            );
            Assert.Contains(upgrade, op => op is AddCheckConstraintOperation);

            PostgresTestDb.Apply(connection, upgrade, Logger);

            Assert.True(LiveExpressionContains(connection, "llm_input_tokens"));
            Assert.False(LiveExpressionContains(connection, "'input_tokens'"));

            var replay = PostgresTestDb.Calculate(
                PostgresTestDb.Inspect(connection, Logger),
                SchemaWithKindCheck("usage_events_kind_check_v2"),
                Logger
            );
            Assert.DoesNotContain(replay, op => op is DropCheckConstraintOperation);
            Assert.DoesNotContain(replay, op => op is AddCheckConstraintOperation);
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(true);
            NpgsqlConnection.ClearPool(connection);
        }
    }

    private static SchemaDefinition SchemaWithKindCheck(string version)
    {
        var expression = version switch
        {
            "usage_events_kind_check_v1" =>
                "kind IN ('request','input_tokens','output_tokens','sandbox_seconds')",
            "usage_events_kind_check_v2" =>
                "kind IN ('llm_request','llm_input_tokens','llm_output_tokens','machine_seconds')",
            _ => throw new ArgumentOutOfRangeException(nameof(version)),
        };
        return new SchemaDefinition
        {
            Name = "issue57",
            Tables =
            [
                new TableDefinition
                {
                    Schema = SchemaName,
                    Name = "usage_events",
                    Columns =
                    [
                        new ColumnDefinition
                        {
                            Name = "id",
                            Type = PortableTypes.Uuid,
                            IsNullable = false,
                        },
                        new ColumnDefinition
                        {
                            Name = "kind",
                            Type = PortableTypes.Text,
                            IsNullable = false,
                        },
                    ],
                    PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
                    CheckConstraints =
                    [
                        new CheckConstraintDefinition
                        {
                            Name = "usage_events_kind_check",
                            Expression = expression,
                        },
                    ],
                },
            ],
        };
    }

    private static bool LiveExpressionContains(NpgsqlConnection connection, string needle)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT pg_get_constraintdef(c.oid)
            FROM pg_constraint c
            JOIN pg_class t ON t.oid = c.conrelid
            JOIN pg_namespace n ON n.oid = t.relnamespace
            WHERE n.nspname = 'public'
              AND t.relname = 'usage_events'
              AND c.contype = 'c'
              AND c.conname = 'usage_events_kind_check'
            """;
        var def = command.ExecuteScalar() as string;
        return def is not null && def.Contains(needle, StringComparison.Ordinal);
    }
}
