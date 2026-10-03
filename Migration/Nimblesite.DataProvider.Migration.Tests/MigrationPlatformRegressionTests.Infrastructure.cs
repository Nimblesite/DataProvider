using System.Data.Common;
using System.Globalization;

namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial record MigrationPlatformRegressionTests
{
    private static readonly object CliConsoleLock = new();

    private Task WithTargetAsync(string provider, Action<MigrationTarget> run) =>
        WithAsyncTargetAsync(
            provider,
            target =>
            {
                run(target);
                return Task.CompletedTask;
            }
        );

    private async Task WithAsyncTargetAsync(string provider, Func<MigrationTarget, Task> run)
    {
        if (provider == "postgres")
        {
            using var connection = await _postgres
                .CreateDatabaseAsync("migration_regression")
                .ConfigureAwait(false);
            await run(
                    new MigrationTarget(provider, "public", connection.ConnectionString, connection)
                )
                .ConfigureAwait(false);
            return;
        }
        if (provider == "sqlserver")
        {
            var connectionString = await _sqlServer
                .CreateDatabaseConnectionStringAsync()
                .ConfigureAwait(false);
            using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
            await connection.OpenAsync().ConfigureAwait(false);
            await run(new MigrationTarget(provider, "dbo", connectionString, connection))
                .ConfigureAwait(false);
            return;
        }
        Assert.Equal("sqlite", provider);
        var path = Path.Combine(Path.GetTempPath(), $"migration_regression_{Guid.NewGuid():N}.db");
        try
        {
            using var sqlite = new SqliteConnection(
                new SqliteConnectionStringBuilder
                {
                    DataSource = path,
                    ForeignKeys = true,
                }.ConnectionString
            );
            await sqlite.OpenAsync().ConfigureAwait(false);
            using var pragma = sqlite.CreateCommand();
            pragma.CommandText = "PRAGMA foreign_keys";
            Assert.Equal(
                1L,
                Assert.IsType<long>(await pragma.ExecuteScalarAsync().ConfigureAwait(false))
            );
            await run(new MigrationTarget(provider, "main", path, sqlite)).ConfigureAwait(false);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void Migrate(
        MigrationTarget target,
        SchemaDefinition desired,
        bool allowDestructive = false
    )
    {
        var result = RunMigrate(target, desired, allowDestructive);
        Assert.True(result.ExitCode == 0, $"{target.Provider} migration failed: {result.Output}");
        Assert.DoesNotContain(
            "SCHEMA INTEGRITY CHECK FAILED",
            result.Output,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(
            "MIG-E-RLS-MSSQL-UNSUPPORTED",
            result.Output,
            StringComparison.Ordinal
        );
    }

    private static (int ExitCode, string Output) RunMigrate(
        MigrationTarget target,
        SchemaDefinition desired,
        bool allowDestructive
    )
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(
                path,
                SchemaYamlSerializer.ToYaml(WithSchema(desired, target.Schema))
            );
            return RunMigrate(target, path, allowDestructive);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static (int ExitCode, string Output) RunMigrate(
        MigrationTarget target,
        string path,
        bool allowDestructive
    )
    {
        // Implements [MIG-CLI-COMMANDS]: the same YAML CLI path drives every provider.
        lock (CliConsoleLock)
        {
            var originalOut = Console.Out;
            var originalError = Console.Error;
            using var output = new StringWriter(CultureInfo.InvariantCulture);
            Console.SetOut(output);
            Console.SetError(output);
            try
            {
                string[] args = allowDestructive
                    ?
                    [
                        "migrate",
                        "--schema",
                        path,
                        "--provider",
                        target.Provider,
                        "--output",
                        target.Output,
                        "--allow-destructive",
                    ]
                    :
                    [
                        "migrate",
                        "--schema",
                        path,
                        "--provider",
                        target.Provider,
                        "--output",
                        target.Output,
                    ];
                var exitCode = DataProviderMigrate.Program.Main(args);
                return (exitCode, output.ToString());
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
            }
        }
    }

    private static SchemaDefinition WithSchema(SchemaDefinition definition, string schema) =>
        definition with
        {
            Tables = definition
                .Tables.Select(table =>
                    table with
                    {
                        Schema = schema,
                        ForeignKeys = table
                            .ForeignKeys.Select(fk => fk with { ReferencedSchema = schema })
                            .ToArray(),
                    }
                )
                .ToArray(),
        };

    private sealed record MigrationTarget(
        string Provider,
        string Schema,
        string Output,
        DbConnection Connection
    );
}
