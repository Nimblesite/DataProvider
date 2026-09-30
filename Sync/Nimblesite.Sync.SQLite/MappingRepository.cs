using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Nimblesite.Sync.SQLite;

/// <summary>
/// SQLite repository for sync mapping state and record hashes.
/// Implements spec Section 7.5.2 tables.
/// </summary>
public static class MappingRepository
{
    /// <summary>
    /// Creates, configures, and executes a command, converting SqliteException
    /// into the supplied error result. Shared by all repository operations.
    /// </summary>
    private static TResult Execute<TResult>(
        SqliteConnection connection,
        Action<SqliteCommand> setSql,
        (string Name, object Value)[] parameters,
        Func<SqliteCommand, TResult> execute,
        Func<string, TResult> onError
    )
    {
        try
        {
            using var cmd = connection.CreateCommand();
            setSql(cmd);
            foreach (var (name, value) in parameters)
            {
                cmd.Parameters.AddWithValue(name, value);
            }

            return execute(cmd);
        }
        catch (SqliteException ex)
        {
            return onError(ex.Message);
        }
    }

    /// <summary>
    /// Maps the current reader row to a MappingStateEntry.
    /// </summary>
    private static MappingStateEntry ReadMappingState(SqliteDataReader reader) =>
        new(reader.GetString(0), reader.GetInt64(1), reader.GetString(2), reader.GetInt64(3));

    /// <summary>
    /// Maps the current reader row to a RecordHashEntry.
    /// </summary>
    private static RecordHashEntry ReadRecordHash(SqliteDataReader reader) =>
        new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3));

    /// <summary>
    /// Gets mapping state by ID.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="mappingId">Mapping identifier.</param>
    /// <returns>Mapping state or null if not found.</returns>
    public static MappingStateResult GetMappingState(
        SqliteConnection connection,
        string mappingId
    ) =>
        Execute<MappingStateResult>(
            connection,
            cmd =>
                cmd.CommandText = """
                SELECT mapping_id, last_synced_version, last_sync_timestamp, records_synced
                FROM _sync_mapping_state
                WHERE mapping_id = @mappingId
                """,
            [("@mappingId", mappingId)],
            cmd =>
            {
                using var reader = cmd.ExecuteReader();
                if (!reader.Read())
                {
                    return new MappingStateOk(null);
                }

                return new MappingStateOk(ReadMappingState(reader));
            },
            message => new MappingStateError(
                new SyncErrorDatabase($"Failed to get mapping state: {message}")
            )
        );

    /// <summary>
    /// Gets all mapping states.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <returns>List of mapping states.</returns>
    public static MappingStateListResult GetAllMappingStates(SqliteConnection connection) =>
        Execute<MappingStateListResult>(
            connection,
            cmd =>
                cmd.CommandText = """
                SELECT mapping_id, last_synced_version, last_sync_timestamp, records_synced
                FROM _sync_mapping_state
                ORDER BY mapping_id
                """,
            [],
            cmd =>
            {
                var states = new List<MappingStateEntry>();
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    states.Add(ReadMappingState(reader));
                }

                return new MappingStateListOk(states);
            },
            message => new MappingStateListError(
                new SyncErrorDatabase($"Failed to get mapping states: {message}")
            )
        );

    /// <summary>
    /// Upserts mapping state.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="state">Mapping state to save.</param>
    /// <returns>Success or error.</returns>
    public static BoolSyncResult UpsertMappingState(
        SqliteConnection connection,
        MappingStateEntry state
    ) =>
        Execute<BoolSyncResult>(
            connection,
            cmd =>
                cmd.CommandText = """
                INSERT INTO _sync_mapping_state (mapping_id, last_synced_version, last_sync_timestamp, records_synced)
                VALUES (@mappingId, @version, @timestamp, @records)
                ON CONFLICT (mapping_id) DO UPDATE SET
                    last_synced_version = @version,
                    last_sync_timestamp = @timestamp,
                    records_synced = @records
                """,
            [
                ("@mappingId", state.MappingId),
                ("@version", state.LastSyncedVersion),
                ("@timestamp", state.LastSyncTimestamp),
                ("@records", state.RecordsSynced),
            ],
            cmd =>
            {
                cmd.ExecuteNonQuery();
                return new BoolSyncOk(true);
            },
            message => new BoolSyncError(
                new SyncErrorDatabase($"Failed to upsert mapping state: {message}")
            )
        );

    /// <summary>
    /// Deletes mapping state.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="mappingId">Mapping identifier.</param>
    /// <returns>Success or error.</returns>
    public static BoolSyncResult DeleteMappingState(
        SqliteConnection connection,
        string mappingId
    ) =>
        Execute<BoolSyncResult>(
            connection,
            cmd =>
                cmd.CommandText = "DELETE FROM _sync_mapping_state WHERE mapping_id = @mappingId",
            [("@mappingId", mappingId)],
            cmd =>
            {
                cmd.ExecuteNonQuery();
                return new BoolSyncOk(true);
            },
            message => new BoolSyncError(
                new SyncErrorDatabase($"Failed to delete mapping state: {message}")
            )
        );

    /// <summary>
    /// Gets record hash by mapping ID and source PK.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="mappingId">Mapping identifier.</param>
    /// <param name="sourcePk">Source primary key JSON.</param>
    /// <returns>Record hash or null if not found.</returns>
    public static RecordHashResult GetRecordHash(
        SqliteConnection connection,
        string mappingId,
        string sourcePk
    ) =>
        Execute<RecordHashResult>(
            connection,
            cmd =>
                cmd.CommandText = """
                SELECT mapping_id, source_pk, payload_hash, synced_at
                FROM _sync_record_hashes
                WHERE mapping_id = @mappingId AND source_pk = @sourcePk
                """,
            [("@mappingId", mappingId), ("@sourcePk", sourcePk)],
            cmd =>
            {
                using var reader = cmd.ExecuteReader();
                if (!reader.Read())
                {
                    return new RecordHashOk(null);
                }

                return new RecordHashOk(ReadRecordHash(reader));
            },
            message => new RecordHashError(
                new SyncErrorDatabase($"Failed to get record hash: {message}")
            )
        );

    /// <summary>
    /// Upserts record hash.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="hash">Record hash to save.</param>
    /// <returns>Success or error.</returns>
    public static BoolSyncResult UpsertRecordHash(
        SqliteConnection connection,
        RecordHashEntry hash
    ) =>
        Execute<BoolSyncResult>(
            connection,
            cmd =>
                cmd.CommandText = """
                INSERT INTO _sync_record_hashes (mapping_id, source_pk, payload_hash, synced_at)
                VALUES (@mappingId, @sourcePk, @hash, @syncedAt)
                ON CONFLICT (mapping_id, source_pk) DO UPDATE SET
                    payload_hash = @hash,
                    synced_at = @syncedAt
                """,
            [
                ("@mappingId", hash.MappingId),
                ("@sourcePk", hash.SourcePk),
                ("@hash", hash.PayloadHash),
                ("@syncedAt", hash.SyncedAt),
            ],
            cmd =>
            {
                cmd.ExecuteNonQuery();
                return new BoolSyncOk(true);
            },
            message => new BoolSyncError(
                new SyncErrorDatabase($"Failed to upsert record hash: {message}")
            )
        );

    /// <summary>
    /// Deletes record hash.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="mappingId">Mapping identifier.</param>
    /// <param name="sourcePk">Source primary key JSON.</param>
    /// <returns>Success or error.</returns>
    public static BoolSyncResult DeleteRecordHash(
        SqliteConnection connection,
        string mappingId,
        string sourcePk
    ) =>
        Execute<BoolSyncResult>(
            connection,
            cmd =>
                cmd.CommandText = """
                DELETE FROM _sync_record_hashes
                WHERE mapping_id = @mappingId AND source_pk = @sourcePk
                """,
            [("@mappingId", mappingId), ("@sourcePk", sourcePk)],
            cmd =>
            {
                cmd.ExecuteNonQuery();
                return new BoolSyncOk(true);
            },
            message => new BoolSyncError(
                new SyncErrorDatabase($"Failed to delete record hash: {message}")
            )
        );

    /// <summary>
    /// Deletes all record hashes for a mapping.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="mappingId">Mapping identifier.</param>
    /// <returns>Number of deleted hashes or error.</returns>
    public static IntSyncResult DeleteRecordHashesByMapping(
        SqliteConnection connection,
        string mappingId
    ) =>
        Execute<IntSyncResult>(
            connection,
            cmd =>
                cmd.CommandText = "DELETE FROM _sync_record_hashes WHERE mapping_id = @mappingId",
            [("@mappingId", mappingId)],
            cmd => new IntSyncOk(cmd.ExecuteNonQuery()),
            message => new IntSyncError(
                new SyncErrorDatabase($"Failed to delete record hashes: {message}")
            )
        );

    /// <summary>
    /// Counts record hashes for a mapping.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="mappingId">Mapping identifier.</param>
    /// <returns>Count or error.</returns>
    public static LongSyncResult CountRecordHashes(SqliteConnection connection, string mappingId) =>
        Execute<LongSyncResult>(
            connection,
            cmd =>
                cmd.CommandText =
                    "SELECT COUNT(*) FROM _sync_record_hashes WHERE mapping_id = @mappingId",
            [("@mappingId", mappingId)],
            cmd => new LongSyncOk(
                Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture)
            ),
            message => new LongSyncError(
                new SyncErrorDatabase($"Failed to count record hashes: {message}")
            )
        );
}
