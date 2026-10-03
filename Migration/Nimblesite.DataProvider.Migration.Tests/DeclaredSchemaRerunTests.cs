namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [MIG-VERIFY-BEFORE-UP-TO-DATE] and [MIG-TEST-CROSS-PLATFORM]. Real schemas
// (found via `make clinical`) must verify on the first run and rerun as a no-op:
// - primary key columns left at the default nullability are NOT NULL on the platform
// - a parenthesized default such as (1) is stored without the redundant parentheses
[Collection(MigrationPlatformSuite.Name)]
public sealed record DeclaredSchemaRerunTests
{
    private readonly MigrationPostgresContainerFixture _postgres;
    private readonly SqlServerContainerFixture _sqlServer;

    public DeclaredSchemaRerunTests(
        MigrationPostgresContainerFixture postgres,
        SqlServerContainerFixture sqlServer
    )
    {
        _postgres = postgres;
        _sqlServer = sqlServer;
    }

    private const string PrimaryKeyDefaultNullability = """
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

    private const string ParenthesizedDefault = """
        name: parenthesized_default
        tables:
          - name: parenthesized_default
            columns:
              - name: id
                type: Uuid
                isNullable: false
              - name: attempts
                type: Int
                isNullable: false
                defaultValue: (1)
            primaryKey:
              columns:
                - id
        """;

    [Theory]
    [InlineData("sqlite", PrimaryKeyDefaultNullability)]
    [InlineData("postgres", PrimaryKeyDefaultNullability)]
    [InlineData("sqlserver", PrimaryKeyDefaultNullability)]
    [InlineData("sqlite", ParenthesizedDefault)]
    [InlineData("postgres", ParenthesizedDefault)]
    [InlineData("sqlserver", ParenthesizedDefault)]
    public async Task Migrate_DeclaredSchema_VerifiesAndRerunsCleanly(string provider, string yaml)
    {
        var schemaPath = Path.GetTempFileName();
        var sqlitePath = Path.Combine(Path.GetTempPath(), $"declared_schema_{Guid.NewGuid():N}.db");
        try
        {
            await File.WriteAllTextAsync(schemaPath, yaml).ConfigureAwait(true);
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
            .CreateDatabaseAsync("declared_schema")
            .ConfigureAwait(true);
        return connection.ConnectionString;
    }

    // All CLI calls share the Console gate, including those checking only an exit code.
    private static void AssertMigrates(string provider, string schemaPath, string output) =>
        Assert.Equal(
            0,
            MigrationCliConsole
                .Migrate(schemaPath: schemaPath, provider: provider, output: output)
                .ExitCode
        );
}
