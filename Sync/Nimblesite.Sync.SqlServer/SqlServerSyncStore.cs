using System.Collections.Immutable;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Nimblesite.Sync.Core;
using BoolResult = Outcome.Result<bool, Nimblesite.Sync.Core.SyncError>;
using ChangesResult = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncLogEntry>,
    Nimblesite.Sync.Core.SyncError
>;
using LongResult = Outcome.Result<long, Nimblesite.Sync.Core.SyncError>;
using StringResult = Outcome.Result<string, Nimblesite.Sync.Core.SyncError>;

namespace Nimblesite.Sync.SqlServer;

/// <summary>SQL Server sync state, change log and client storage. Implements [SYNC-HTTP-PROVIDERS].</summary>
public static class SqlServerSyncStore
{
    /// <summary>Reads changes in version order, excluding the supplied starting version.</summary>
    public static ChangesResult FetchChanges(
        SqlConnection connection,
        long fromVersion,
        int limit,
        ILogger? logger = null
    ) =>
        RelationalSyncCommands.Run<IReadOnlyList<SyncLogEntry>>(
            connection: connection,
            configure: command =>
                command.CommandText =
                    "SELECT TOP (@limit) version, table_name, pk_value, operation, payload, origin, timestamp FROM dbo._sync_log WHERE version > @fromVersion ORDER BY version",
            parameters: [("@limit", limit), ("@fromVersion", fromVersion)],
            execute: ReadChanges,
            logger: logger
        );

    /// <summary>Reads the latest change version, or zero for an empty log.</summary>
    public static LongResult GetMaxVersion(SqlConnection connection) =>
        RelationalSyncCommands.Run(
            connection: connection,
            configure: command =>
                command.CommandText = "SELECT COALESCE(MAX(version), 0) FROM dbo._sync_log",
            parameters: [],
            execute: command =>
                Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture)
        );

    /// <summary>Reads this replica's persistent origin identifier.</summary>
    public static StringResult GetOriginId(SqlConnection connection) =>
        RelationalSyncCommands.Run(
            connection: connection,
            configure: command =>
                command.CommandText = "SELECT value FROM dbo._sync_state WHERE [key] = 'origin_id'",
            parameters: [],
            execute: command => command.ExecuteScalar()?.ToString() ?? string.Empty
        );

    /// <summary>Stores this replica's origin identifier.</summary>
    public static BoolResult SetOriginId(SqlConnection connection, string originId) =>
        RelationalSyncCommands.Run(
            connection: connection,
            configure: command =>
                command.CommandText =
                    "UPDATE dbo._sync_state SET value = @origin WHERE [key] = 'origin_id'",
            parameters: [("@origin", originId)],
            execute: command => command.ExecuteNonQuery() == 1
        );

    /// <summary>Creates or updates a client's acknowledged version while preserving its creation time.</summary>
    public static BoolResult UpsertClient(SqlConnection connection, SyncClient client) =>
        RelationalSyncCommands.Run(
            connection: connection,
            configure: command =>
                command.CommandText = """
                SET XACT_ABORT ON;
                BEGIN TRANSACTION;
                UPDATE dbo._sync_clients WITH (UPDLOCK, SERIALIZABLE)
                SET last_sync_version = @version, last_sync_timestamp = @timestamp WHERE origin_id = @origin;
                IF @@ROWCOUNT = 0
                    INSERT INTO dbo._sync_clients (origin_id, last_sync_version, last_sync_timestamp, created_at)
                    VALUES (@origin, @version, @timestamp, @created);
                COMMIT TRANSACTION;
                """,
            parameters:
            [
                ("@origin", client.OriginId),
                ("@version", client.LastSyncVersion),
                ("@timestamp", client.LastSyncTimestamp),
                ("@created", client.CreatedAt),
            ],
            execute: command =>
            {
                command.ExecuteNonQuery();
                return true;
            }
        );

    /// <summary>Changes trigger suppression only for this connection.</summary>
    public static BoolResult SetSuppression(SqlConnection connection, bool enabled) =>
        RelationalSyncCommands.Run(
            connection: connection,
            configure: command =>
                command.CommandText =
                    "EXEC sys.sp_set_session_context @key = N'sync_active', @value = @active",
            parameters: [("@active", enabled ? 1 : 0)],
            execute: command =>
            {
                command.ExecuteNonQuery();
                return true;
            }
        );

    private static IReadOnlyList<SyncLogEntry> ReadChanges(System.Data.Common.DbCommand command)
    {
        var entries = ImmutableArray.CreateBuilder<SyncLogEntry>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
            entries.Add(
                new SyncLogEntry(
                    Version: reader.GetInt64(0),
                    TableName: reader.GetString(1),
                    PkValue: reader.GetString(2),
                    Operation: Enum.Parse<SyncOperation>(
                        value: reader.GetString(3),
                        ignoreCase: true
                    ),
                    Payload: reader.IsDBNull(4) ? null : reader.GetString(4),
                    Origin: reader.GetString(5),
                    Timestamp: reader.GetString(6)
                )
            );
        return entries.ToImmutable();
    }
}
