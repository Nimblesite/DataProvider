namespace Nimblesite.DataProvider.Migration.Tests;

// Tests [RLS-MSSQL] fail-closed guard until native SQL Server RLS is implemented.

/// <summary>
/// CLI tests for unsupported SQL Server RLS migration attempts.
/// </summary>
public sealed partial class DataProviderMigrateRlsUnsupportedTests
{
    [Fact]
    public void Migrate_SqlServerProviderWithRlsSchema_ReturnsCanonicalUnsupportedError()
    {
        var schemaPath = WriteTempSchemaFile(contents: RlsSchemaYaml());

        try
        {
            var result = RunMigrate(schemaPath: schemaPath);

            global::Nimblesite.DataProvider.Migration.Tests.DataProviderMigrateIntegrityTests.VerifyReusableWorkflow15(
                input0: result,
                value0: 1,
                value1: "MIG-E-RLS-MSSQL-UNSUPPORTED"
            );
            Assert.Contains(
                expectedSubstring: "Native SQL Server RLS migrations are not yet supported",
                actualString: result.Output,
                comparisonType: StringComparison.Ordinal
            );
            Assert.DoesNotContain(
                expectedSubstring: "package does not exist",
                actualString: result.Output,
                comparisonType: StringComparison.Ordinal
            );
        }
        finally
        {
            File.Delete(path: schemaPath);
        }
    }

    private static (int ExitCode, string Output) RunMigrate(string schemaPath) =>
        MigrationCliConsole.Migrate(
            schemaPath: schemaPath,
            provider: "sqlserver",
            output: "unused"
        );

    private static string WriteTempSchemaFile(string contents) =>
        MigrationCliConsole.WriteTempSchemaFile(
            prefix: "dataprovider-rls-unsupported",
            contents: contents
        );

    private static string RlsSchemaYaml() => Data_RlsSchemaYaml_1;
}
