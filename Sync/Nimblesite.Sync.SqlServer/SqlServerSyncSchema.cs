using System.Collections.Immutable;
using Microsoft.Data.SqlClient;
using Nimblesite.DataProvider.Migration.Core;
using Nimblesite.DataProvider.Migration.SqlServer;
using Nimblesite.Sync.Core;
using BoolResult = Outcome.Result<bool, Nimblesite.Sync.Core.SyncError>;
using OperationsOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.DataProvider.Migration.Core.SchemaOperation>,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Ok<
    System.Collections.Generic.IReadOnlyList<Nimblesite.DataProvider.Migration.Core.SchemaOperation>,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>;
using SchemaOk = Outcome.Result<
    Nimblesite.DataProvider.Migration.Core.SchemaDefinition,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Ok<
    Nimblesite.DataProvider.Migration.Core.SchemaDefinition,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>;

namespace Nimblesite.Sync.SqlServer;

/// <summary>Creates SQL Server sync storage using DataProvider migrations. Implements [SYNC-HTTP-PROVIDERS].</summary>
public static class SqlServerSyncSchema
{
    /// <summary>Creates missing sync tables and initializes state without resetting existing data.</summary>
    public static BoolResult CreateSchema(SqlConnection connection)
    {
        if (SqlServerSchemaInspector.Inspect(connection: connection) is not SchemaOk current)
            return Failure(message: "Could not inspect sync schema");
        if (
            SchemaDiff.Calculate(current: current.Value, desired: Definition())
            is not OperationsOk operations
        )
            return Failure(message: "Could not calculate sync migration");
        var migration = MigrationRunner.Apply(
            connection: connection,
            operations: operations.Value,
            options: new MigrationOptions(),
            generateDdl: SqlServerDdlGenerator.Generate
        );
        return migration.Match<BoolResult>(
            _ => Initialize(connection: connection),
            error => Failure(message: error.Message)
        );
    }

    private static SchemaDefinition Definition() =>
        SyncSchemaDefinition.Definition with
        {
            Tables = SyncSchemaDefinition
                .Definition.Tables.Select(table =>
                    table with
                    {
                        Schema = "dbo",
                        Columns = table
                            .Columns.Select(column =>
                                column.Type is TextType
                                && column.Name != "payload"
                                && column.Name != "pk_value"
                                && column.Name != "filter"
                                    ? column with
                                    {
                                        Type = PortableTypes.NVarChar(200),
                                    }
                                    : column
                            )
                            .ToImmutableArray(),
                    }
                )
                .ToImmutableArray(),
        };

    private static BoolResult Initialize(SqlConnection connection) =>
        RelationalSyncCommands.Run(
            connection: connection,
            configure: command =>
                command.CommandText = """
                IF NOT EXISTS (SELECT 1 FROM dbo._sync_session) INSERT INTO dbo._sync_session (sync_active) VALUES (0);
                IF NOT EXISTS (SELECT 1 FROM dbo._sync_state WHERE [key] = 'origin_id') INSERT INTO dbo._sync_state ([key], value) VALUES ('origin_id', '');
                IF NOT EXISTS (SELECT 1 FROM dbo._sync_state WHERE [key] = 'last_server_version') INSERT INTO dbo._sync_state ([key], value) VALUES ('last_server_version', '0');
                IF NOT EXISTS (SELECT 1 FROM dbo._sync_state WHERE [key] = 'last_push_version') INSERT INTO dbo._sync_state ([key], value) VALUES ('last_push_version', '0');
                """,
            parameters: [],
            execute: command =>
            {
                command.ExecuteNonQuery();
                return true;
            }
        );

    private static BoolResult Failure(string message) =>
        new BoolResult.Error<bool, SyncError>(new SyncErrorDatabase(message));
}
