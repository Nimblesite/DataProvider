using System.Globalization;

namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [MIG-VERIFY-BEFORE-UP-TO-DATE] and [MIG-TEST-CROSS-PLATFORM]. Primary key
// columns are NOT NULL on every platform even when the schema leaves isNullable at its
// default, so the first run must verify and a rerun must be a no-op.
[Collection(MigrationPlatformSuite.Name)]
public sealed record PrimaryKeyNullabilityTests
{
    private readonly MigrationPostgresContainerFixture _postgres;
    private readonly SqlServerContainerFixture _sqlServer;

    public PrimaryKeyNullabilityTests(
        MigrationPostgresContainerFixture postgres,
        SqlServerContainerFixture sqlServer
    )
    {
        _postgres = postgres;
        _sqlServer = sqlServer;
    }

    private const string Yaml = """
        name: pk_nullability
        tables:
          - name: pk_default_nullability
            columns:
              - name: id
                type: Uuid
              - name: label
                type: Text
            primaryKey:
              columns:
                - id
        """;

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    public async Task Migrate_PrimaryKeyWithDefaultNullability_VerifiesAndRerunsCleanly(
        string provider
    )
    {
        var schemaPath = Path.GetTempFileName();
        var sqlitePath = Path.Combine(Path.GetTempPath(), $"pk_nullability_{Guid.NewGuid():N}.db");
        try
        {
            await File.WriteAllTextAsync(schemaPath, Yaml).ConfigureAwait(true);
            var output = provider switch
            {
                "postgres" => await PostgresConnectionString().ConfigureAwait(true),
                "sqlserver" => await _sqlServer
                    .CreateDatabaseConnectionStringAsync()
                    .ConfigureAwait(true),
                _ => sqlitePath,
            };
            AssertMigrates(provider, schemaPath, output);
            AssertMigrates(provider, schemaPath, output);
        }
        finally
        {
            File.Delete(schemaPath);
            File.Delete(sqlitePath);
        }
    }

    private async Task<string> PostgresConnectionString()
    {
        using var connection = await _postgres
            .CreateDatabaseAsync("pk_nullability")
            .ConfigureAwait(true);
        return connection.ConnectionString;
    }

    private static void AssertMigrates(string provider, string schemaPath, string output)
    {
        var originalOut = Console.Out;
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        Console.SetOut(writer);
        int exitCode;
        try
        {
            exitCode = DataProviderMigrate.Program.Main([
                "migrate",
                "--schema",
                schemaPath,
                "--provider",
                provider,
                "--output",
                output,
            ]);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
        Assert.True(exitCode == 0, $"{provider} migration failed: {writer}");
        Assert.DoesNotContain(
            "SCHEMA INTEGRITY CHECK FAILED",
            writer.ToString(),
            StringComparison.Ordinal
        );
    }
}
