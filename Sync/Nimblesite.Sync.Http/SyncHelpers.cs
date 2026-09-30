using Microsoft.Data.Sqlite;
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
            var applied = 0;
            foreach (var change in changes)
            {
                if (change.Origin == originId)
                    continue;

                var entry = ToSyncLogEntry(change);

                var result = SQLite.ChangeApplierSQLite.ApplyChange(conn, entry);
                if (result is Outcome.Result<bool, SyncError>.Ok<bool, SyncError>)
                    applied++;
            }
            return applied;
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
            var applied = 0;
            foreach (var change in changes)
            {
                if (change.Origin == originId)
                    continue;

                var entry = ToSyncLogEntry(change);

                var result = Postgres.PostgresChangeApplier.ApplyChange(conn, entry, logger);
                if (result is Outcome.Result<bool, SyncError>.Ok<bool, SyncError>)
                    applied++;
            }
            return applied;
        }
        finally
        {
            Postgres.PostgresSyncSession.DisableSuppression(conn);
        }
    }

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

    private static SyncLogEntry ToSyncLogEntry(SyncLogEntryDto change) =>
        new(
            change.Version,
            change.TableName,
            change.PkValue,
            Enum.Parse<SyncOperation>(change.Operation, true),
            change.Payload,
            change.Origin,
            change.Timestamp
        );
}
