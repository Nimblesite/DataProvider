using System.Data.Common;
using SyncClientError = Outcome.Result<
    Nimblesite.Sync.Core.SyncClient?,
    Nimblesite.Sync.Core.SyncError
>.Error<Nimblesite.Sync.Core.SyncClient?, Nimblesite.Sync.Core.SyncError>;
using SyncClientListError = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncClient>,
    Nimblesite.Sync.Core.SyncError
>.Error<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncClient>,
    Nimblesite.Sync.Core.SyncError
>;
using SyncClientListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncClient>,
    Nimblesite.Sync.Core.SyncError
>.Ok<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncClient>,
    Nimblesite.Sync.Core.SyncError
>;
using SyncClientListResult = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncClient>,
    Nimblesite.Sync.Core.SyncError
>;
using SyncClientOk = Outcome.Result<
    Nimblesite.Sync.Core.SyncClient?,
    Nimblesite.Sync.Core.SyncError
>.Ok<Nimblesite.Sync.Core.SyncClient?, Nimblesite.Sync.Core.SyncError>;
using SyncClientResult = Outcome.Result<
    Nimblesite.Sync.Core.SyncClient?,
    Nimblesite.Sync.Core.SyncError
>;

namespace Nimblesite.Sync.Core;

// Implements [SYNC-CLIENT-SHARED-STORAGE].
internal static class SyncClientStorage
{
    internal static SyncClientListResult GetAll<TException>(
        DbConnection connection,
        string failurePrefix
    )
        where TException : Exception
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT origin_id, last_sync_version, last_sync_timestamp, created_at
                FROM _sync_clients
                ORDER BY last_sync_version ASC
                """;
            var clients = new List<SyncClient>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                clients.Add(ReadClient(reader: reader));
            }

            return new SyncClientListOk(clients);
        }
        catch (TException ex)
        {
            return new SyncClientListError(new SyncErrorDatabase($"{failurePrefix}: {ex.Message}"));
        }
    }

    internal static SyncClientResult GetByOrigin<TException>(
        DbConnection connection,
        string originId
    )
        where TException : Exception
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT origin_id, last_sync_version, last_sync_timestamp, created_at
                FROM _sync_clients
                WHERE origin_id = @originId
                """;
            Bind(command: cmd, name: "@originId", value: originId);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                return new SyncClientOk(null);
            }

            var client = ReadClient(reader: reader);
            return new SyncClientOk(client);
        }
        catch (TException ex)
        {
            return new SyncClientError(
                new SyncErrorDatabase($"Failed to get sync client: {ex.Message}")
            );
        }
    }

    internal static BoolSyncResult Upsert<TException>(
        DbConnection connection,
        SyncClient client,
        string failurePrefix
    )
        where TException : Exception
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO _sync_clients (origin_id, last_sync_version, last_sync_timestamp, created_at)
                VALUES (@originId, @lastVersion, @lastTimestamp, @createdAt)
                ON CONFLICT(origin_id) DO UPDATE SET
                    last_sync_version = @lastVersion,
                    last_sync_timestamp = @lastTimestamp
                """;
            Bind(command: cmd, name: "@originId", value: client.OriginId);
            Bind(command: cmd, name: "@lastVersion", value: client.LastSyncVersion);
            Bind(command: cmd, name: "@lastTimestamp", value: client.LastSyncTimestamp);
            Bind(command: cmd, name: "@createdAt", value: client.CreatedAt);
            cmd.ExecuteNonQuery();
            return new BoolSyncOk(true);
        }
        catch (TException ex)
        {
            return new BoolSyncError(new SyncErrorDatabase($"{failurePrefix}: {ex.Message}"));
        }
    }

    internal static BoolSyncResult Delete<TException>(DbConnection connection, string originId)
        where TException : Exception
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM _sync_clients WHERE origin_id = @originId";
            Bind(command: cmd, name: "@originId", value: originId);
            cmd.ExecuteNonQuery();
            return new BoolSyncOk(true);
        }
        catch (TException ex)
        {
            return new BoolSyncError(
                new SyncErrorDatabase($"Failed to delete sync client: {ex.Message}")
            );
        }
    }

    internal static IntSyncResult DeleteMultiple<TException>(
        DbConnection connection,
        IEnumerable<string> originIds,
        string failurePrefix
    )
        where TException : Exception
    {
        try
        {
            var deleted = 0;
            foreach (var originId in originIds)
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "DELETE FROM _sync_clients WHERE origin_id = @originId";
                Bind(command: cmd, name: "@originId", value: originId);
                deleted += cmd.ExecuteNonQuery();
            }

            return new IntSyncOk(deleted);
        }
        catch (TException ex)
        {
            return new IntSyncError(new SyncErrorDatabase($"{failurePrefix}: {ex.Message}"));
        }
    }

    private static SyncClient ReadClient(DbDataReader reader) =>
        new(
            OriginId: reader.GetString(0),
            LastSyncVersion: reader.GetInt64(1),
            LastSyncTimestamp: reader.GetString(2),
            CreatedAt: reader.GetString(3)
        );

    private static void Bind(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(value: parameter);
    }
}
