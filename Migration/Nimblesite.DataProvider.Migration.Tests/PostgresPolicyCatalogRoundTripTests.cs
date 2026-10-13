// Implements [MIG-TEST-SCHEMA-FIXTURES].
namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [RLS-DIFF]. PostgreSQL catalog deparsing must not cause false policy drift.
[Collection(PostgresTestSuite.Name)]
public sealed partial class PostgresPolicyCatalogRoundTripTests(PostgresContainerFixture fixture)
{
    [Fact]
    public void ExistingDatabase_CatalogDeparseRerunsCleanlyAndRoleLiteralChanges()
    {
        var connectionString = fixture.CreateDatabaseConnectionString("policy_catalog_roundtrip");
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        var initialPath = Path.GetTempFileName();
        var changedPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(initialPath, SchemaYamlSerializer.ToYaml(PolicySchema("member")));
            File.WriteAllText(changedPath, SchemaYamlSerializer.ToYaml(PolicySchema("owner")));

            RunCliAndAssert(connectionString, initialPath, "Schema integrity check passed");

            RunCliAndAssert(
                connectionString,
                initialPath,
                "Schema is up to date — no operations needed"
            );

            RunCliAndAssert(connectionString, changedPath, "AlterRlsPolicyOperation");
            var rolePredicate = ReadPredicate(connection, "role_policy");
            Assert.Contains("'owner'", rolePredicate, StringComparison.Ordinal);
            Assert.DoesNotContain("'member'", rolePredicate, StringComparison.Ordinal);

            RunCliAndAssert(
                connectionString,
                changedPath,
                "Schema is up to date — no operations needed"
            );
        }
        finally
        {
            File.Delete(initialPath);
            File.Delete(changedPath);
        }
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void RunCliAndAssert(
        string connectionString,
        string schemaPath,
        string expectedOutput
    )
    {
        var result = RunCli(connectionString, schemaPath);
        Assert.True(result.Code == 0, result.Output);
        Assert.Contains(expectedOutput, result.Output);
    }

    private static (int Code, string Output) RunCli(string connectionString, string path) =>
        MigrationCliConsole.Migrate(
            schemaPath: path,
            provider: "postgres",
            output: connectionString
        );

    private static string ReadPredicate(NpgsqlConnection connection, string name)
    {
        return Assert.IsType<string>(
            global::Tests.Shared.DatabaseCommands.Scalar(
                connection: connection,
                configure: command =>
                    command.CommandText = """
                    SELECT qual FROM pg_policies
                    WHERE schemaname = 'public' AND tablename = 'children' AND policyname = @name
                    """,
                parameters: () => [("name", name)]
            )
        );
    }

    private static ColumnDefinition Id() =>
        SchemaTestData.Column(name: "id", type: PortableTypes.Uuid, isNullable: false);
}
