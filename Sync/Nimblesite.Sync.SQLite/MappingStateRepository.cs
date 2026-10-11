using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Nimblesite.Sync.SQLite;

/// <summary>
/// Repository for mapping state and record hash tracking.
/// Implements spec Section 7.5.2 - Tracking Tables.
/// </summary>
public static class MappingStateRepository
{
    /// <summary>
    /// Gets mapping state by ID.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="mappingId">Mapping identifier.</param>
    /// <returns>Mapping state or null if not found.</returns>
    public static MappingStateResult GetMappingState(SqliteConnection connection, string mappingId)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT mapping_id, last_synced_version, last_sync_timestamp, records_synced
                FROM _sync_mapping_state
                WHERE mapping_id = @mappingId
                """;
            cmd.Parameters.AddWithValue("@mappingId", mappingId);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new MappingStateOk(
                    new MappingStateEntry(
                        reader.GetString(0),
                        reader.GetInt64(1),
                        reader.GetString(2),
                        reader.GetInt64(3)
                    )
                );
            }

            return new MappingStateOk(null);
        }
        catch (SqliteException ex)
        {
            return new MappingStateError(
                new SyncErrorDatabase($"Failed to get mapping state: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Gets all mapping states.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <returns>List of mapping states.</returns>
    public static MappingStateListResult GetAllMappingStates(SqliteConnection connection)
    {
        try
        {
            var states = new List<MappingStateEntry>();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT mapping_id, last_synced_version, last_sync_timestamp, records_synced
                FROM _sync_mapping_state
                ORDER BY mapping_id
                """;

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                states.Add(
                    new MappingStateEntry(
                        reader.GetString(0),
                        reader.GetInt64(1),
                        reader.GetString(2),
                        reader.GetInt64(3)
                    )
                );
            }

            return new MappingStateListOk(states);
        }
        catch (SqliteException ex)
        {
            return new MappingStateListError(
                new SyncErrorDatabase($"Failed to get mapping states: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Upserts a mapping state entry.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="state">State to upsert.</param>
    /// <returns>Success or error.</returns>
    public static BoolSyncResult UpsertMappingState(
        SqliteConnection connection,
        MappingStateEntry state
    ) =>
        SyncCommandExecution.Write<SqliteException>(
            connection: connection,
            setSql: command =>
                command.CommandText = """
                INSERT INTO _sync_mapping_state (mapping_id, last_synced_version, last_sync_timestamp, records_synced)
                VALUES (@mappingId, @version, @timestamp, @records)
                ON CONFLICT(mapping_id) DO UPDATE SET
                    last_synced_version = @version,
                    last_sync_timestamp = @timestamp,
                    records_synced = @records
                """,
            parameters: () =>
                [
                    ("@mappingId", state.MappingId),
                    ("@version", state.LastSyncedVersion),
                    ("@timestamp", state.LastSyncTimestamp),
                    ("@records", state.RecordsSynced),
                ],
            failurePrefix: "Failed to upsert mapping state"
        );

    /// <summary>
    /// Deletes a mapping state entry.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="mappingId">Mapping identifier to delete.</param>
    /// <returns>Success or error.</returns>
    public static BoolSyncResult DeleteMappingState(
        SqliteConnection connection,
        string mappingId
    ) =>
        SyncCommandExecution.Write<SqliteException>(
            connection: connection,
            setSql: command =>
                command.CommandText =
                    "DELETE FROM _sync_mapping_state WHERE mapping_id = @mappingId",
            parameters: () => [("@mappingId", mappingId)],
            failurePrefix: "Failed to delete mapping state"
        );

    /// <summary>
    /// Gets record hash for hash-based sync tracking.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="mappingId">Mapping identifier.</param>
    /// <param name="sourcePk">Source primary key JSON.</param>
    /// <returns>Record hash entry or null.</returns>
    public static RecordHashResult GetRecordHash(
        SqliteConnection connection,
        string mappingId,
        string sourcePk
    )
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT mapping_id, source_pk, payload_hash, synced_at
                FROM _sync_record_hashes
                WHERE mapping_id = @mappingId AND source_pk = @sourcePk
                """;
            cmd.Parameters.AddWithValue("@mappingId", mappingId);
            cmd.Parameters.AddWithValue("@sourcePk", sourcePk);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new RecordHashOk(
                    new RecordHashEntry(
                        reader.GetString(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetString(3)
                    )
                );
            }

            return new RecordHashOk(null);
        }
        catch (SqliteException ex)
        {
            return new RecordHashError(
                new SyncErrorDatabase($"Failed to get record hash: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Upserts a record hash entry.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="entry">Hash entry to upsert.</param>
    /// <returns>Success or error.</returns>
    public static BoolSyncResult UpsertRecordHash(
        SqliteConnection connection,
        RecordHashEntry entry
    ) =>
        SyncCommandExecution.Write<SqliteException>(
            connection: connection,
            setSql: command =>
                command.CommandText = """
                INSERT INTO _sync_record_hashes (mapping_id, source_pk, payload_hash, synced_at)
                VALUES (@mappingId, @sourcePk, @hash, @syncedAt)
                ON CONFLICT(mapping_id, source_pk) DO UPDATE SET
                    payload_hash = @hash,
                    synced_at = @syncedAt
                """,
            parameters: () =>
                [
                    ("@mappingId", entry.MappingId),
                    ("@sourcePk", entry.SourcePk),
                    ("@hash", entry.PayloadHash),
                    ("@syncedAt", entry.SyncedAt),
                ],
            failurePrefix: "Failed to upsert record hash"
        );

    /// <summary>
    /// Deletes record hashes for a mapping.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="mappingId">Mapping identifier.</param>
    /// <returns>Number of deleted records or error.</returns>
    public static IntSyncResult DeleteRecordHashesForMapping(
        SqliteConnection connection,
        string mappingId
    ) =>
        SyncCommandExecution.WriteCount<SqliteException>(
            connection: connection,
            setSql: command =>
                command.CommandText =
                    "DELETE FROM _sync_record_hashes WHERE mapping_id = @mappingId",
            parameters: () => [("@mappingId", mappingId)],
            failurePrefix: "Failed to delete record hashes"
        );

    /// <summary>
    /// Gets the minimum last_synced_version across all active mappings.
    /// Used for tombstone retention calculation.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <returns>Minimum version or 0 if no mappings.</returns>
    public static LongSyncResult GetMinSyncedVersion(SqliteConnection connection)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT MIN(last_synced_version) FROM _sync_mapping_state";
            var result = cmd.ExecuteScalar();

            if (result is null || result == DBNull.Value)
            {
                return new LongSyncOk(0);
            }

            return new LongSyncOk(Convert.ToInt64(result, CultureInfo.InvariantCulture));
        }
        catch (SqliteException ex)
        {
            return new LongSyncError(
                new SyncErrorDatabase($"Failed to get min synced version: {ex.Message}")
            );
        }
    }
}
