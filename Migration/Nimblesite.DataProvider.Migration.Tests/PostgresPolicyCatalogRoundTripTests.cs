// Implements [MIG-TEST-SCHEMA-FIXTURES].
namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [RLS-DIFF]. PostgreSQL catalog deparsing must not cause false policy drift.
[Collection(PostgresTestSuite.Name)]
public sealed class PostgresPolicyCatalogRoundTripTests(PostgresContainerFixture fixture)
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

            var initial = RunCli(connectionString, initialPath);
            Assert.True(initial.Code == 0, initial.Output);
            Assert.Contains("Schema integrity check passed", initial.Output);

            var repeat = RunCli(connectionString, initialPath);
            Assert.True(repeat.Code == 0, repeat.Output);
            Assert.Contains("Schema is up to date — no operations needed", repeat.Output);

            var changed = RunCli(connectionString, changedPath);
            Assert.True(changed.Code == 0, changed.Output);
            Assert.Contains("AlterRlsPolicyOperation", changed.Output);
            var rolePredicate = ReadPredicate(connection, "role_policy");
            Assert.Contains("'owner'", rolePredicate, StringComparison.Ordinal);
            Assert.DoesNotContain("'member'", rolePredicate, StringComparison.Ordinal);

            var changedRepeat = RunCli(connectionString, changedPath);
            Assert.True(changedRepeat.Code == 0, changedRepeat.Output);
            Assert.Contains("Schema is up to date — no operations needed", changedRepeat.Output);
        }
        finally
        {
            File.Delete(initialPath);
            File.Delete(changedPath);
        }
    }

    private static (int Code, string Output) RunCli(string connectionString, string path) =>
        MigrationCliConsole.Migrate(
            schemaPath: path,
            provider: "postgres",
            output: connectionString
        );

    private static string ReadPredicate(NpgsqlConnection connection, string name)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT qual FROM pg_policies
            WHERE schemaname = 'public' AND tablename = 'children' AND policyname = @name
            """;
        command.Parameters.AddWithValue("name", name);
        return Assert.IsType<string>(command.ExecuteScalar());
    }

    private static SchemaDefinition PolicySchema(string secondRole) =>
        SchemaTestData.Schema(
            name: "policy_catalog_roundtrip",
            functions:
            [
                SchemaTestData.PostgresFunction(
                    name: "app_is_system",
                    returns: "text",
                    body: "SELECT 'true'::text"
                ),
            ],
            tables:
            [
                SchemaTestData.Table(
                    name: "parents",
                    columns: [Id()],
                    primaryKey: new PrimaryKeyDefinition { Columns = ["id"] }
                ),
                SchemaTestData.Table(
                    name: "children",
                    columns:
                    [
                        Id(),
                        SchemaTestData.Column(name: "parent_id", type: PortableTypes.Uuid),
                        SchemaTestData.Column(name: "role", type: PortableTypes.Text),
                    ],
                    primaryKey: new PrimaryKeyDefinition { Columns = ["id"] },
                    rowLevelSecurity: new RlsPolicySetDefinition
                    {
                        Policies =
                        [
                            SchemaTestData.RlsPolicy(
                                name: "system_all",
                                operations: [RlsOperation.All],
                                usingLql: "app_is_system() = 'true'",
                                withCheckLql: "app_is_system() = 'true'"
                            ),
                            SchemaTestData.RlsPolicy(
                                name: "parent_policy",
                                operations: [RlsOperation.Select],
                                usingLql: "exists(parents |> filter(fn(p) => p.id = parent_id))"
                            ),
                            SchemaTestData.RlsPolicy(
                                name: "role_policy",
                                operations: [RlsOperation.Select],
                                usingLql: $"role in ('admin', '{secondRole}')"
                            ),
                        ],
                    }
                ),
            ]
        );

    private static ColumnDefinition Id() =>
        SchemaTestData.Column(name: "id", type: PortableTypes.Uuid, isNullable: false);
}
