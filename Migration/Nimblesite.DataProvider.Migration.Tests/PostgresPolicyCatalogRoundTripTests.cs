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

    private static (int Code, string Output) RunCli(string connectionString, string path)
    {
        var original = Console.Out;
        using var output = new StringWriter();
        Console.SetOut(output);
        try
        {
            var code = DataProviderMigrate.Program.Main([
                "migrate",
                "--schema",
                path,
                "--provider",
                "postgres",
                "--output",
                connectionString,
            ]);
            return (code, output.ToString());
        }
        finally
        {
            Console.SetOut(original);
        }
    }

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
        new()
        {
            Name = "policy_catalog_roundtrip",
            Functions =
            [
                new PostgresFunctionDefinition
                {
                    Name = "app_is_system",
                    Returns = "text",
                    Body = "SELECT 'true'::text",
                },
            ],
            Tables =
            [
                new TableDefinition
                {
                    Name = "parents",
                    Columns = [Id()],
                    PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
                },
                new TableDefinition
                {
                    Name = "children",
                    Columns =
                    [
                        Id(),
                        new ColumnDefinition { Name = "parent_id", Type = PortableTypes.Uuid },
                        new ColumnDefinition { Name = "role", Type = PortableTypes.Text },
                    ],
                    PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
                    RowLevelSecurity = new RlsPolicySetDefinition
                    {
                        Policies =
                        [
                            new RlsPolicyDefinition
                            {
                                Name = "system_all",
                                Operations = [RlsOperation.All],
                                UsingLql = "app_is_system() = 'true'",
                                WithCheckLql = "app_is_system() = 'true'",
                            },
                            new RlsPolicyDefinition
                            {
                                Name = "parent_policy",
                                Operations = [RlsOperation.Select],
                                UsingLql = "exists(parents |> filter(fn(p) => p.id = parent_id))",
                            },
                            new RlsPolicyDefinition
                            {
                                Name = "role_policy",
                                Operations = [RlsOperation.Select],
                                UsingLql = $"role in ('admin', '{secondRole}')",
                            },
                        ],
                    },
                },
            ],
        };

    private static ColumnDefinition Id() =>
        new()
        {
            Name = "id",
            Type = PortableTypes.Uuid,
            IsNullable = false,
        };
}
