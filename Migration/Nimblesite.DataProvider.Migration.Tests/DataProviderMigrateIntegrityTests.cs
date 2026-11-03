using static global::Tests.Shared.ResultAssertions;

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
public sealed partial class DataProviderMigrateIntegrityTests(PostgresContainerFixture fixture)
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
            var result = VerifySharedChecks5(input0: schemaPath);
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

    /// <summary>
    /// Implements [MIG-VERIFY-BEFORE-UP-TO-DATE]: a clean diff doesn't prove the
    /// live schema matches the desired one. The CLI must NOT print "Schema is up
    /// to date" until the integrity verifier confirms it; doing so when the
    /// verifier subsequently fails is a lie. Concrete drift used here: the diff
    /// is non-destructive and cannot alter an existing column default, so when
    /// the YAML declares a different default than the live column, Calculate
    /// returns zero operations but the verifier reports drift.
    /// </summary>
    [Fact]
    public void Migrate_WhenDiffIsCleanButIntegrityFails_DoesNotClaimSchemaUpToDate()
    {
        var baseline =
            Fixture_Migrate_WhenDiffIsCleanButIntegrityFails_DoesNotClaimSchemaUpToDate_1();
        ApplySchema(schema: baseline);

        var schemaPath = WriteTempSchemaFile(
            contents: Data_Migrate_WhenDiffIsCleanButIntegrityFails_DoesNotClaimSchemaUpToDate_1
        );

        try
        {
            var result = VerifySharedChecks5(input0: schemaPath);
            Assert.False(
                condition: result.Output.Contains(
                    value: "Schema is up to date",
                    comparisonType: StringComparison.Ordinal
                ),
                userMessage: "CLI must not claim 'Schema is up to date' before the "
                    + "integrity check confirms it.\n"
                    + result.Output
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

            VerifyReusableWorkflow15(
                input0: first,
                value0: 0,
                value1: "AddUniqueConstraintOperation"
            );
            AssertUniqueConstraintExists();

            var second = RunMigrate(schemaPath: schemaPath);

            VerifyReusableWorkflow15(input0: second, value0: 0, value1: "Schema is up to date");
        }
        finally
        {
            File.Delete(path: schemaPath);
        }
    }

    /// <summary>
    /// Implements [MIG-POSTGRES-CUSTOM-SCHEMA]: the CLI must inspect the schema the
    /// YAML declares, not a hardcoded "public", so migrating into a custom schema
    /// and re-running succeeds with zero operations and a truthful up-to-date claim.
    /// </summary>
    [Fact]
    public void Migrate_CustomSchemaFirstRunAppliesAndRerunReportsZeroOperations()
    {
        ExecuteNonQuery(sql: "CREATE SCHEMA IF NOT EXISTS registry");
        var schemaPath = WriteTempSchemaFile(contents: Data_CustomRegistrySchemaYaml_1);

        try
        {
            var first = RunMigrate(schemaPath: schemaPath);
            VerifyReusableWorkflow15(
                input0: first,
                value0: 0,
                value1: "Migration completed successfully"
            );

            var second = RunMigrate(schemaPath: schemaPath);
            VerifyReusableWorkflow15(
                input0: second,
                value0: 0,
                value1: "Schema is up to date — no operations needed"
            );
        }
        finally
        {
            File.Delete(path: schemaPath);
        }
    }

    /// <summary>
    /// Implements [MIG-POSTGRES-CATALOG-EQUIVALENT-EXPRESSIONS]: the actual Osprey
    /// package-registry schema declares partial unique indexes and check
    /// constraints whose catalog forms differ textually from the YAML
    /// (literal ::text casts, dropped redundant parens, INTERVAL '24 hours'
    /// read back as '24:00:00'::interval). Both sides must be canonicalized
    /// through PostgreSQL's own parser so the first run applies and the rerun
    /// verifies clean with zero operations.
    /// </summary>
    [Fact]
    public void Migrate_OspreyRegistryOwnershipSchemaAppliesAndRerunReportsZeroOperations()
    {
        ExecuteNonQuery(sql: "CREATE SCHEMA IF NOT EXISTS registry");
        var schemaPath = WriteTempSchemaFile(
            contents: Data_OspreyRegistryOwnershipSchemaYaml_1
        );

        try
        {
            var first = RunMigrate(schemaPath: schemaPath);
            VerifyReusableWorkflow15(
                input0: first,
                value0: 0,
                value1: "Migration completed successfully"
            );

            var second = RunMigrate(schemaPath: schemaPath);
            VerifyReusableWorkflow15(
                input0: second,
                value0: 0,
                value1: "Schema is up to date — no operations needed"
            );
        }
        finally
        {
            File.Delete(path: schemaPath);
        }
    }

    /// <summary>
    /// Implements [MIG-POSTGRES-CATALOG-EQUIVALENT-EXPRESSIONS]: catalog
    /// equivalence must not swallow real predicate changes. A semantically
    /// different interval ('12 hours' vs '24 hours') and a different partial
    /// index filter ('pending' vs 'active') must produce operations instead of
    /// a false "up to date" verdict.
    /// </summary>
    [Fact]
    public void Migrate_GenuinelyDifferentPredicatesAreNotTreatedAsEquivalent()
    {
        ExecuteNonQuery(sql: "CREATE SCHEMA IF NOT EXISTS registry");
        var schemaPath = WriteTempSchemaFile(
            contents: Data_OspreyRegistryOwnershipSchemaYaml_1
        );
        var tamperedPath = WriteTempSchemaFile(
            contents: Data_OspreyRegistryOwnershipTamperedPredicatesYaml_1
        );

        try
        {
            VerifyReusableWorkflow15(
                input0: RunMigrate(schemaPath: schemaPath),
                value0: 0,
                value1: "Migration completed successfully"
            );

            var tampered = RunMigrate(schemaPath: tamperedPath);
            Assert.Equal(expected: 0, actual: tampered.ExitCode);
            Assert.DoesNotContain(
                expectedSubstring: "Schema is up to date — no operations needed",
                actualString: tampered.Output,
                comparisonType: StringComparison.Ordinal
            );
            Assert.Contains(
                expectedSubstring: "Phase: all — applying",
                actualString: tampered.Output,
                comparisonType: StringComparison.Ordinal
            );
        }
        finally
        {
            File.Delete(path: schemaPath);
            File.Delete(path: tamperedPath);
        }
    }

    /// <summary>
    /// Implements [MIG-POSTGRES-CUSTOM-SCHEMA]: tables spanning multiple target
    /// schemas cannot be merged into one inspection; the CLI must reject
    /// explicitly instead of silently verifying only one schema.
    /// </summary>
    [Fact]
    public void Migrate_MultipleTargetSchemasIsRejectedExplicitly()
    {
        var schemaPath = WriteTempSchemaFile(contents: Data_TwoSchemaYaml_1);

        try
        {
            var result = RunMigrate(schemaPath: schemaPath);
            Assert.Equal(expected: 1, actual: result.ExitCode);
            Assert.Contains(
                expectedSubstring: "Error: PostgreSQL migration supports exactly one target schema",
                actualString: result.Output,
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

    private static string DesiredAgentConfigsYaml() => Data_DesiredAgentConfigsYaml_1;

    private static string DesiredAgentConfigsWithUniqueYaml() =>
        Data_DesiredAgentConfigsWithUniqueYaml_1;

    private static void ConfigureNameColumn(ColumnBuilder builder, bool nameIsNullable)
    {
        if (!nameIsNullable)
        {
            builder.NotNull();
        }
    }

    private void ApplySchema(SchemaDefinition schema)
    {
        var operations = SqliteTestDb.CalculateChanges(
            inspect: () => (Ok(actual: PostgresSchemaInspector.Inspect(_connection))).Value,
            desired: schema,
            allowDestructive: false,
            logger: null
        );
        var result = PostgresTestDb.TryApply(
            connection: _connection,
            operations: operations,
            logger: _logger
        );

        PostgresTestDb.AssertApplyResult(apply: result);
    }

    private void ExecuteNonQuery(string sql)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private (int ExitCode, string Output) RunMigrate(string schemaPath) =>
        MigrationCliConsole.Migrate(
            schemaPath: schemaPath,
            provider: "postgres",
            output: _connectionString
        );

    private static string WriteTempSchemaFile(string contents) =>
        MigrationCliConsole.WriteTempSchemaFile(
            prefix: "dataprovider-integrity",
            contents: contents
        );

    private void AssertUniqueConstraintExists()
    {
        var schema = (Ok(actual: PostgresSchemaInspector.Inspect(_connection))).Value;
        var table = Assert.Single(schema.Tables, t => t.Name == "agent_configs");
        var unique = Assert.Single(
            table.UniqueConstraints,
            uc => uc.Name == "uq_agent_configs_tenant_name"
        );

        Assert.Equal(expected: ["tenant_id", "name"], actual: unique.Columns);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private (int ExitCode, string Output) VerifySharedChecks5(string input0)
    {
        var result = RunMigrate(schemaPath: input0);

        Assert.Equal(expected: 1, actual: result.ExitCode);
        Assert.True(
            condition: result.Output.Contains(
                value: "SCHEMA INTEGRITY CHECK FAILED",
                comparisonType: StringComparison.Ordinal
            ),
            userMessage: result.Output
        );

        return result;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyReusableWorkflow15(
        (int ExitCode, string Output) input0,
        int value0,
        string value1
    )
    {
        Assert.Equal(expected: value0, actual: input0.ExitCode);
        Assert.Contains(
            expectedSubstring: value1,
            actualString: input0.Output,
            comparisonType: StringComparison.Ordinal
        );
    }
}
