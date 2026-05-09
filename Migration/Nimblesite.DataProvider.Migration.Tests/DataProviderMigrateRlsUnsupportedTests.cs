using System.Globalization;

namespace Nimblesite.DataProvider.Migration.Tests;

// Tests [RLS-MSSQL] SQL Server package absence guard from docs/specs/rls-spec.md.

/// <summary>
/// CLI tests for unsupported SQL Server RLS migration attempts.
/// </summary>
public sealed class DataProviderMigrateRlsUnsupportedTests
{
    [Fact]
    public void Migrate_SqlServerProviderWithRlsSchema_ReturnsCanonicalUnsupportedError()
    {
        var schemaPath = WriteTempSchemaFile(contents: RlsSchemaYaml());

        try
        {
            var result = RunMigrate(schemaPath: schemaPath);

            Assert.Equal(expected: 1, actual: result.ExitCode);
            Assert.Contains(
                expectedSubstring: "MIG-E-RLS-MSSQL-UNSUPPORTED",
                actualString: result.Output,
                comparisonType: StringComparison.Ordinal
            );
            Assert.Contains(
                expectedSubstring: "Nimblesite.DataProvider.Migration.SqlServer package does not exist",
                actualString: result.Output,
                comparisonType: StringComparison.Ordinal
            );
        }
        finally
        {
            File.Delete(path: schemaPath);
        }
    }

    private static (int ExitCode, string Output) RunMigrate(string schemaPath)
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
                    "sqlserver",
                    "--output",
                    "unused",
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
                $"dataprovider-rls-unsupported-{Guid.NewGuid():N}.yaml"
            )
        );
        File.WriteAllText(path: schemaPath, contents: contents);
        return schemaPath;
    }

    private static string RlsSchemaYaml() =>
        """
            name: sqlserver_rls_guard
            tables:
              - name: documents
                schema: public
                columns:
                  - name: id
                    type: Uuid
                    isNullable: false
                  - name: owner_id
                    type: Uuid
                    isNullable: false
                primaryKey:
                  columns:
                    - id
                rowLevelSecurity:
                  policies:
                    - name: owner_isolation
                      operations:
                        - Select
                      using: owner_id = current_user_id()
            """;
}
