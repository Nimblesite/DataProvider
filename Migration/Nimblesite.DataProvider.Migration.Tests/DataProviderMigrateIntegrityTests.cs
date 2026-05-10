using System.Globalization;

namespace Nimblesite.DataProvider.Migration.Tests;

/// <summary>
/// CLI integrity tests for DataProviderMigrate.
/// </summary>
[Collection(PostgresTestSuite.Name)]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Usage",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Disposed via IAsyncLifetime.DisposeAsync"
)]
public sealed class DataProviderMigrateIntegrityTests(PostgresContainerFixture fixture)
    : IAsyncLifetime
{
    private NpgsqlConnection _connection = null!;
    private string _connectionString = string.Empty;
    private readonly ILogger _logger = NullLogger.Instance;

    public async Task InitializeAsync()
    {
        var fixtureConnectionString = await fixture
            .CreateDatabaseConnectionStringAsync(namePrefix: "migrate_integrity_test")
            .ConfigureAwait(continueOnCapturedContext: false);
        _connectionString = new NpgsqlConnectionStringBuilder(fixtureConnectionString)
        {
            Pooling = false,
        }.ConnectionString;
        _connection = new NpgsqlConnection(connectionString: _connectionString);
        await _connection.OpenAsync().ConfigureAwait(continueOnCapturedContext: false);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync().ConfigureAwait(continueOnCapturedContext: false);
    }

    [Fact]
    public void Migrate_PostApplyIntegrityCheckFailsWhenColumnNullabilityDrifts()
    {
        var baseline = CreateAgentConfigsSchema(nameIsNullable: true);
        ApplySchema(schema: baseline);

        var schemaPath = WriteTempSchemaFile(contents: DesiredAgentConfigsYaml());

        try
        {
            var result = RunMigrate(schemaPath: schemaPath);

            Assert.Equal(expected: 1, actual: result.ExitCode);
            Assert.True(
                condition: result.Output.Contains(
                    value: "SCHEMA INTEGRITY CHECK FAILED",
                    comparisonType: StringComparison.Ordinal
                ),
                userMessage: result.Output
            );
            Assert.True(
                condition: result.Output.Contains(
                    value: "public.agent_configs.name: nullability expected NOT NULL but found NULL",
                    comparisonType: StringComparison.Ordinal
                ),
                userMessage: result.Output
            );
        }
        finally
        {
            File.Delete(path: schemaPath);
        }
    }

    [Fact]
    public void Migrate_AddsDeclaredCompositeUniqueConstraintToExistingPostgresTable()
    {
        var baseline = CreateAgentConfigsSchema(nameIsNullable: false);
        ApplySchema(schema: baseline);

        var schemaPath = WriteTempSchemaFile(contents: DesiredAgentConfigsWithUniqueYaml());

        try
        {
            var first = RunMigrate(schemaPath: schemaPath);

            Assert.Equal(expected: 0, actual: first.ExitCode);
            Assert.Contains(
                expectedSubstring: "AddUniqueConstraintOperation",
                actualString: first.Output,
                comparisonType: StringComparison.Ordinal
            );
            AssertUniqueConstraintExists();

            var second = RunMigrate(schemaPath: schemaPath);

            Assert.Equal(expected: 0, actual: second.ExitCode);
            Assert.Contains(
                expectedSubstring: "Schema is up to date",
                actualString: second.Output,
                comparisonType: StringComparison.Ordinal
            );
        }
        finally
        {
            File.Delete(path: schemaPath);
        }
    }

    private static SchemaDefinition CreateAgentConfigsSchema(bool nameIsNullable) =>
        Schema
            .Define(name: "nap")
            .Table(
                schema: "public",
                name: "agent_configs",
                configure: t =>
                    t.Column(
                            name: "id",
                            type: PortableTypes.Uuid,
                            configure: c =>
                                c.PrimaryKey().Default(defaultValue: "gen_random_uuid()")
                        )
                        .Column(
                            name: "tenant_id",
                            type: PortableTypes.Uuid,
                            configure: c => c.NotNull()
                        )
                        .Column(
                            name: "name",
                            type: PortableTypes.Text,
                            configure: c =>
                                ConfigureNameColumn(builder: c, nameIsNullable: nameIsNullable)
                        )
            )
            .Build();

    private static string DesiredAgentConfigsYaml() =>
        """
            name: nap
            tables:
              - name: agent_configs
                schema: public
                columns:
                  - name: id
                    type: Uuid
                    isNullable: false
                  - name: tenant_id
                    type: Uuid
                    isNullable: false
                  - name: name
                    type: Text
                    isNullable: false
                primaryKey:
                  columns:
                    - id
            """;

    private static string DesiredAgentConfigsWithUniqueYaml() =>
        """
            name: nap
            tables:
              - name: agent_configs
                schema: public
                columns:
                  - name: id
                    type: Uuid
                    isNullable: false
                  - name: tenant_id
                    type: Uuid
                    isNullable: false
                  - name: name
                    type: Text
                    isNullable: false
                primaryKey:
                  columns:
                    - id
                uniqueConstraints:
                  - name: uq_agent_configs_tenant_name
                    columns:
                      - tenant_id
                      - name
            """;

    private static void ConfigureNameColumn(ColumnBuilder builder, bool nameIsNullable)
    {
        if (!nameIsNullable)
        {
            builder.NotNull();
        }
    }

    private void ApplySchema(SchemaDefinition schema)
    {
        var current = ((SchemaResultOk)PostgresSchemaInspector.Inspect(_connection)).Value;
        var operations = (
            (OperationsResultOk)SchemaDiff.Calculate(current: current, desired: schema)
        ).Value;
        var result = MigrationRunner.Apply(
            connection: _connection,
            operations: operations,
            generateDdl: PostgresDdlGenerator.Generate,
            options: MigrationOptions.Default,
            logger: _logger
        );

        Assert.True(
            condition: result is MigrationApplyResultOk,
            userMessage: $"Baseline migration failed: {(result as MigrationApplyResultError)?.Value}"
        );
    }

    private (int ExitCode, string Output) RunMigrate(string schemaPath)
    {
        var originalOut = Console.Out;
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        Console.SetOut(output);
        try
        {
            var exitCode = DataProviderMigrate.Program.Main(
                args:
                [
                    "migrate",
                    "--schema",
                    schemaPath,
                    "--provider",
                    "postgres",
                    "--output",
                    _connectionString,
                ]
            );
            return (exitCode, output.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    private static string WriteTempSchemaFile(string contents)
    {
        var schemaPath = Path.Combine(
            Path.GetTempPath(),
            string.Create(
                CultureInfo.InvariantCulture,
                $"dataprovider-integrity-{Guid.NewGuid():N}.yaml"
            )
        );
        File.WriteAllText(path: schemaPath, contents: contents);
        return schemaPath;
    }

    private void AssertUniqueConstraintExists()
    {
        var schema = ((SchemaResultOk)PostgresSchemaInspector.Inspect(_connection)).Value;
        var table = Assert.Single(schema.Tables, t => t.Name == "agent_configs");
        var unique = Assert.Single(
            table.UniqueConstraints,
            uc => uc.Name == "uq_agent_configs_tenant_name"
        );

        Assert.Equal(expected: ["tenant_id", "name"], actual: unique.Columns);
    }
}
