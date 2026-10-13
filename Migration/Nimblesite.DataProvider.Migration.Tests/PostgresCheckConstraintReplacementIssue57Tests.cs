// Implements [MIG-TEST-SCHEMA-FIXTURES].
namespace Nimblesite.DataProvider.Migration.Tests;

/// <summary>
/// Implements [MIG-CHECK-CONSTRAINT-EXPRESSION-DRIFT] (#57): when a YAML
/// CHECK constraint changes its predicate but keeps the same name, the
/// migrator must drop the live constraint and add the desired one. Closes
/// the silent prod outage where a renamed enum-style CHECK was skipped
/// because the diff matched on name only.
/// </summary>
[Collection(PostgresTestSuite.Name)]
public sealed partial class PostgresCheckConstraintReplacementIssue57Tests(
    PostgresContainerFixture fixture
)
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
            var upgrade = VerifyReusableWorkflow26(
                input0: LiveExpressionContains,
                input1: connection,
                input2: Logger,
                input3: SchemaWithKindCheck,
                value0: "input_tokens",
                value1: "llm_input_tokens"
            );
            SchemaDiffAssertions.AssertContainsOperation<DropCheckConstraintOperation>(
                upgrade,
                drop => drop.ConstraintName == "usage_events_kind_check"
            );
            Assert.Contains(upgrade, op => op is AddCheckConstraintOperation);

            PostgresTestDb.Apply(connection, upgrade, Logger);

            var replay = VerifyReusableWorkflow26(
                input0: LiveExpressionContains,
                input1: connection,
                input2: Logger,
                input3: SchemaWithKindCheck,
                value0: "llm_input_tokens",
                value1: "'input_tokens'"
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
        return Fixture_SchemaWithKindCheck_1(expression: expression);
    }

    private static bool LiveExpressionContains(NpgsqlConnection connection, string needle)
    {
        var def =
            global::Tests.Shared.DatabaseCommands.Scalar(
                connection: connection,
                configure: command => command.CommandText = Data_LiveExpressionContains_1
            ) as string;

        return def is not null && def.Contains(needle, StringComparison.Ordinal);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static IReadOnlyList<SchemaOperation> VerifyReusableWorkflow26(
        Func<NpgsqlConnection, string, bool> input0,
        NpgsqlConnection input1,
        ILogger input2,
        Func<string, SchemaDefinition> input3,
        string value0,
        string value1
    )
    {
        Assert.True(input0(arg1: input1, arg2: value0));
        Assert.False(input0(arg1: input1, arg2: value1));

        var upgrade = PostgresTestDb.Calculate(
            PostgresTestDb.Inspect(input1, input2),
            input3(arg: "usage_events_kind_check_v2"),
            input2
        );

        return upgrade;
    }
}
