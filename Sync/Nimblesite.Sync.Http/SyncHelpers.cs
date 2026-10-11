using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Nimblesite.Sync.SqlServer;
using Npgsql;

// TODO: Logging!!
#pragma warning disable IDE0060 // Remove unused parameter

namespace Nimblesite.Sync.Http;

/// <summary>
/// Helper methods for sync database operations.
/// Static methods that work with both SQLite and PostgreSQL.
/// </summary>
public static class SyncHelpers
{
    /// <summary>
    /// Fetches changes from the sync log for the specified database type.
    /// </summary>
    public static List<SyncLogEntry> FetchChanges(
        string connectionString,
        string dbType,
        long fromVersion,
        int batchSize,
        ILogger logger
    ) =>
        dbType.ToLowerInvariant() switch
        {
            "sqlite" => FetchChangesFromSqlite(connectionString, fromVersion, batchSize, logger),
            "postgres" => FetchChangesFromPostgres(
                connectionString,
                fromVersion,
                batchSize,
                logger
            ),
            "sqlserver" or "mssql" => WithSqlServer(
                connectionString,
                connection =>
                    SqlServerSyncStore
                        .FetchChanges(
                            connection: connection,
                            fromVersion: fromVersion,
                            limit: batchSize,
                            logger: logger
                        )
                        .Match<List<SyncLogEntry>>(ok => [.. ok], _ => [])
            ),
            _ => throw new ArgumentException($"Unknown database type: {dbType}"),
        };

    /// <summary>
    /// Applies changes to the database for the specified database type.
    /// </summary>
    public static int ApplyChanges(
        string connectionString,
        string dbType,
        List<SyncLogEntryDto> changes,
        string originId,
        ILogger logger
    ) =>
        dbType.ToLowerInvariant() switch
        {
            "sqlite" => ApplyChangesToSqlite(connectionString, changes, originId, logger),
            "postgres" => ApplyChangesToPostgres(connectionString, changes, originId, logger),
            "sqlserver" or "mssql" => ApplyChangesToSqlServer(
                connectionString,
                changes,
                originId,
                logger
            ),
            _ => throw new ArgumentException($"Unknown database type: {dbType}"),
        };

    /// <summary>
    /// Upserts a sync client for the specified database type.
    /// </summary>
    public static bool UpsertClient(
        string connectionString,
        string dbType,
        SyncClient client,
        ILogger logger
    ) =>
        dbType.ToLowerInvariant() switch
        {
            "sqlite" => UpsertClientSqlite(connectionString, client, logger),
            "postgres" => UpsertClientPostgres(connectionString, client, logger),
            "sqlserver" or "mssql" => WithSqlServer(
                connectionString,
                connection =>
                    SqlServerSyncStore
                        .UpsertClient(connection: connection, client: client)
                        .Match(ok => ok, _ => false)
            ),
            _ => throw new ArgumentException($"Unknown database type: {dbType}"),
        };

    /// <summary>
    /// Gets the maximum sync version for the specified database type.
    /// </summary>
    public static long GetMaxVersion(string connectionString, string dbType, ILogger logger) =>
        dbType.ToLowerInvariant() switch
        {
            "sqlite" => GetMaxVersionSqlite(connectionString, logger),
            "postgres" => GetMaxVersionPostgres(connectionString, logger),
            "sqlserver" or "mssql" => WithSqlServer(
                connectionString,
                connection =>
                    SqlServerSyncStore.GetMaxVersion(connection: connection).Match(ok => ok, _ => 0)
            ),
            _ => throw new ArgumentException($"Unknown database type: {dbType}"),
        };

    private static List<SyncLogEntry> FetchChangesFromSqlite(
        string connectionString,
        long fromVersion,
        int batchSize,
        ILogger logger
    )
    {
        using var conn = new SqliteConnection(connectionString);
        conn.Open();
        return SQLite
            .SyncLogRepository.FetchChanges(conn, fromVersion, batchSize)
            .Match<List<SyncLogEntry>>(ok => [.. ok], _ => []);
    }

    private static List<SyncLogEntry> FetchChangesFromPostgres(
        string connectionString,
        long fromVersion,
        int batchSize,
        ILogger logger
    )
    {
        using var conn = new NpgsqlConnection(connectionString);
        conn.Open();
        return Postgres
            .PostgresSyncLogRepository.FetchChanges(conn, fromVersion, batchSize)
            .Match<List<SyncLogEntry>>(ok => [.. ok], _ => []);
    }

    private static int ApplyChangesToSqlite(
        string connectionString,
        List<SyncLogEntryDto> changes,
        string originId,
        ILogger logger
    )
    {
        using var conn = new SqliteConnection(connectionString);
        conn.Open();

        SQLite.SyncSessionManager.EnableSuppression(conn);

        try
        {
            return ApplyChangesCore(
                changes: changes,
                originId: originId,
                applyChange: entry =>
                    SQLite.ChangeApplierSQLite.ApplyChange(conn, entry)
                        is Outcome.Result<bool, SyncError>.Ok<bool, SyncError> { Value: true }
            );
        }
        finally
        {
            SQLite.SyncSessionManager.DisableSuppression(conn);
        }
    }

    private static int ApplyChangesToPostgres(
        string connectionString,
        List<SyncLogEntryDto> changes,
        string originId,
        ILogger logger
    )
    {
        using var conn = new NpgsqlConnection(connectionString);
        conn.Open();

        Postgres.PostgresSyncSession.EnableSuppression(conn);

        try
        {
            return ApplyChangesCore(
                changes: changes,
                originId: originId,
                applyChange: entry =>
                    Postgres.PostgresChangeApplier.ApplyChange(conn, entry, logger)
                        is Outcome.Result<bool, SyncError>.Ok<bool, SyncError> { Value: true }
            );
        }
        finally
        {
            Postgres.PostgresSyncSession.DisableSuppression(conn);
        }
    }

    private static int ApplyChangesCore(
        IReadOnlyList<SyncLogEntryDto> changes,
        string originId,
        Func<SyncLogEntry, bool> applyChange
    )
    {
        var applied = 0;
        foreach (var change in changes)
        {
            if (change.Origin == originId)
                continue;

            if (applyChange(ToSyncLogEntry(change: change)))
                applied++;
        }

        return applied;
    }

    private static SyncLogEntry ToSyncLogEntry(SyncLogEntryDto change) =>
        new(
            Version: change.Version,
            TableName: change.TableName,
            PkValue: change.PkValue,
            Operation: Enum.Parse<SyncOperation>(value: change.Operation, ignoreCase: true),
            Payload: change.Payload,
            Origin: change.Origin,
            Timestamp: change.Timestamp
        );

    private static bool UpsertClientSqlite(
        string connectionString,
        SyncClient client,
        ILogger logger
    )
    {
        using var conn = new SqliteConnection(connectionString);
        conn.Open();
        var result = SQLite.SyncClientRepository.Upsert(conn, client);
        return result is Outcome.Result<bool, SyncError>.Ok<bool, SyncError>;
    }

    private static bool UpsertClientPostgres(
        string connectionString,
        SyncClient client,
        ILogger logger
    )
    {
        using var conn = new NpgsqlConnection(connectionString);
        conn.Open();
        var result = Postgres.PostgresSyncClientRepository.Upsert(conn, client);
        return result is Outcome.Result<bool, SyncError>.Ok<bool, SyncError>;
    }

    private static long GetMaxVersionSqlite(string connectionString, ILogger logger)
    {
        using var conn = new SqliteConnection(connectionString);
        conn.Open();
        return SQLite.SyncLogRepository.GetMaxVersion(conn).Match(ok => ok, _ => 0);
    }

    private static long GetMaxVersionPostgres(string connectionString, ILogger logger)
    {
        using var conn = new NpgsqlConnection(connectionString);
        conn.Open();
        return Postgres.PostgresSyncLogRepository.GetMaxVersion(conn).Match(ok => ok, _ => 0);
    }

    private static T WithSqlServer<T>(string connectionString, Func<SqlConnection, T> run)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        return run(connection);
    }

    private static int ApplyChangesToSqlServer(
        string connectionString,
        List<SyncLogEntryDto> changes,
        string originId,
        ILogger logger
    ) =>
        WithSqlServer(
            connectionString,
            connection =>
            {
                var suppression = SqlServerSyncStore.SetSuppression(
                    connection: connection,
                    enabled: true
                );
                if (
                    suppression
                    is not Outcome.Result<bool, SyncError>.Ok<bool, SyncError> { Value: true }
                )
                    return 0;
                try
                {
                    return ApplyChangesCore(
                        changes: changes,
                        originId: originId,
                        applyChange: entry =>
                            SqlServerChangeApplier.ApplyChange(
                                connection: connection,
                                entry: entry,
                                logger: logger
                            )
                                is Outcome.Result<bool, SyncError>.Ok<bool, SyncError>
                                {
                                    Value: true
                                }
                    );
                }
                finally
                {
                    SqlServerSyncStore.SetSuppression(connection: connection, enabled: false);
                }
            }
        );
}
