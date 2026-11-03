#if !AOT
using Microsoft.Data.SqlClient;
using Nimblesite.DataProvider.Migration.Core;
using Nimblesite.DataProvider.Migration.SqlServer;
#endif

namespace DataProviderMigrate;

// Implements [MIG-AOT-SQLSERVER]. Microsoft.Data.SqlClient is not trim or Native AOT
// compatible, so the native binary leaves out the SQL Server provider. SQL Server
// migrations remain available in the managed `dotnet tool` and the NuGet packages.
public static partial class Program
{
#if AOT
    private static int ShowSqlServerUnavailable()
    {
        Console.WriteLine(
            "Error: the 'sqlserver' provider is not available in the native (AOT) build.\n"
                + "Microsoft.Data.SqlClient does not support Native AOT.\n"
                + "Use the managed dotnet tool 'DataProviderMigrate migrate --provider sqlserver ...' instead."
        );
        return 1;
    }
#else
    // Implements [MIG-SQLSERVER].
    private static int MigrateSqlServerDatabase(
        SchemaDefinition schema,
        string connectionString,
        bool allowDestructive,
        MigratePhase phase
    )
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();
            Console.WriteLine("Connected to SQL Server database");

            return ApplyDiff(
                schema,
                allowDestructive,
                phase,
                () => SqlServerSchemaInspector.Inspect(connection),
                ops =>
                    MigrationRunner.Apply(
                        connection,
                        ops,
                        SqlServerDdlGenerator.Generate,
                        new MigrationOptions { AllowDestructive = allowDestructive }
                    ),
                (_, desired) => SqlServerSchemaNormalizer.Normalize(desired)
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: SQL Server connection/migration failed: {ex}");
            return 1;
        }
    }
#endif
}
