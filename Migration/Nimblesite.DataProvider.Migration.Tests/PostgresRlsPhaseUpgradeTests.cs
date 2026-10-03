namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [RLS-DIFF]. Policy alterations and replacements belong to the RLS CLI phase.
[Collection(PostgresTestSuite.Name)]
public sealed class PostgresRlsPhaseUpgradeTests(PostgresContainerFixture fixture)
{
    [Fact]
    public void RlsPhase_UpdatesPredicateAndScopeWhileStructuralPhaseLeavesPoliciesAlone()
    {
        var connectionString = fixture.CreateDatabaseConnectionString("rls_phase_upgrade");
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        var original = PolicySchema(changed: false);
        var desired = PolicySchema(changed: true);
        var originalPath = Path.GetTempFileName();
        var desiredPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(originalPath, SchemaYamlSerializer.ToYaml(original));
            File.WriteAllText(desiredPath, SchemaYamlSerializer.ToYaml(desired));

            Assert.Equal(0, RunCli(connectionString, originalPath, "all"));
            Assert.Equal(0, RunCli(connectionString, desiredPath, "structural"));
            var originalPredicate = ReadPolicy(connection, "owner_policy");
            var originalScope = ReadPolicy(connection, "scope_policy");
            Assert.Contains(
                "IS NOT NULL",
                originalPredicate.Using,
                StringComparison.OrdinalIgnoreCase
            );
            Assert.Equal("SELECT", originalScope.Command);

            Assert.Equal(0, RunCli(connectionString, desiredPath, "rls"));
            var changedPredicate = ReadPolicy(connection, "owner_policy");
            var changedScope = ReadPolicy(connection, "scope_policy");
            Assert.Contains("IS NULL", changedPredicate.Using, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(
                "IS NULL",
                changedPredicate.WithCheck,
                StringComparison.OrdinalIgnoreCase
            );
            Assert.DoesNotContain(
                "IS NOT NULL",
                changedPredicate.Using,
                StringComparison.OrdinalIgnoreCase
            );
            Assert.Equal("DELETE", changedScope.Command);

            var live = Assert
                .IsType<SchemaResultOk>(PostgresSchemaInspector.Inspect(connection, "public"))
                .Value;
            Assert.Empty(
                Assert.IsType<OperationsResultOk>(SchemaDiff.Calculate(live, desired)).Value
            );
            Assert.Equal(0, RunCli(connectionString, desiredPath, "rls"));
            Assert.Equal(changedPredicate, ReadPolicy(connection, "owner_policy"));
            Assert.Equal(changedScope, ReadPolicy(connection, "scope_policy"));
        }
        finally
        {
            File.Delete(originalPath);
            File.Delete(desiredPath);
        }
    }

    private static int RunCli(string connectionString, string schemaPath, string phase) =>
        MigrationCliConsole
            .Migrate(
                schemaPath: schemaPath,
                provider: "postgres",
                output: connectionString,
                phase: phase
            )
            .ExitCode;

    private static (string Command, string? Using, string? WithCheck) ReadPolicy(
        NpgsqlConnection connection,
        string name
    )
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT cmd, qual, with_check FROM pg_policies
            WHERE schemaname = 'public' AND tablename = 'documents' AND policyname = @name
            """;
        command.Parameters.AddWithValue("name", name);
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read(), $"Missing policy {name}");
        var policy = (
            Command: reader.GetString(0),
            Using: reader.IsDBNull(1) ? null : reader.GetString(1),
            WithCheck: reader.IsDBNull(2) ? null : reader.GetString(2)
        );
        Assert.False(reader.Read(), $"Duplicate policy {name}");
        return policy;
    }

    private static SchemaDefinition PolicySchema(bool changed) =>
        new()
        {
            Name = "rls_phase_upgrade",
            Tables =
            [
                new TableDefinition
                {
                    Name = "documents",
                    Columns =
                    [
                        new ColumnDefinition
                        {
                            Name = "id",
                            Type = PortableTypes.Uuid,
                            IsNullable = false,
                        },
                        new ColumnDefinition { Name = "owner_id", Type = PortableTypes.Uuid },
                    ],
                    PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
                    RowLevelSecurity = new RlsPolicySetDefinition
                    {
                        Policies =
                        [
                            new RlsPolicyDefinition
                            {
                                Name = "owner_policy",
                                Operations = [RlsOperation.All],
                                UsingSql = changed ? "owner_id IS NULL" : "owner_id IS NOT NULL",
                                WithCheckSql = changed
                                    ? "owner_id IS NULL"
                                    : "owner_id IS NOT NULL",
                            },
                            new RlsPolicyDefinition
                            {
                                Name = "scope_policy",
                                Operations = [changed ? RlsOperation.Delete : RlsOperation.Select],
                                UsingSql = "TRUE",
                            },
                        ],
                    },
                },
            ],
        };
}
