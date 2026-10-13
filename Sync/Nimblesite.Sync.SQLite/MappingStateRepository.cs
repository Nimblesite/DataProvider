using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Nimblesite.Sync.SQLite;

/// <summary>
/// Repository for mapping state and record hash tracking.
/// Implements spec Section 7.5.2 - Tracking Tables.
/// </summary>
public static partial class MappingStateRepository
{
    /// <summary>
    /// Gets mapping state by ID.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="mappingId">Mapping identifier.</param>
    /// <returns>Mapping state or null if not found.</returns>
    public static MappingStateResult GetMappingState(SqliteConnection connection, string mappingId)
    {
        return SyncCommandExecution.Read<SqliteException, MappingStateResult>(
            connection: connection,
            setSql: command =>
                command.CommandText = global::Nimblesite
                    .Sync
                    .SQLite
                    .MappingStateRepository
                    .SqlSpecification1,
            parameters: () => [("@mappingId", mappingId)],
            read: command =>
            {
                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    return new MappingStateOk(ReadMappingStateEntry(reader: reader));
                }

                return new MappingStateOk(null);
            },
            failure: ex =>
            {
                return new MappingStateError(
                    new SyncErrorDatabase($"Failed to get mapping state: {ex.Message}")
                );
            }
        );
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
            cmd.CommandText = global::Nimblesite
                .Sync
                .SQLite
                .MappingStateRepository
                .SqlSpecification2;

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                states.Add(ReadMappingStateEntry(reader: reader));
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
        WriteMappingState(
            connection: connection,
            state: state,
            setSql: command =>
                command.CommandText = global::Nimblesite
                    .Sync
                    .SQLite
                    .MappingStateRepository
                    .SqlSpecification3
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
        return SyncCommandExecution.Read<SqliteException, RecordHashResult>(
            connection: connection,
            setSql: command =>
                command.CommandText = global::Nimblesite
                    .Sync
                    .SQLite
                    .MappingStateRepository
                    .SqlSpecification4,
            parameters: () => [("@mappingId", mappingId), ("@sourcePk", sourcePk)],
            read: command =>
            {
                using var reader = command.ExecuteReader();
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
            },
            failure: ex =>
            {
                return new RecordHashError(
                    new SyncErrorDatabase($"Failed to get record hash: {ex.Message}")
                );
            }
        );
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
        WriteRecordHash(
            connection: connection,
            entry: entry,
            setSql: command =>
                command.CommandText = global::Nimblesite
                    .Sync
                    .SQLite
                    .MappingStateRepository
                    .SqlSpecification5
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
        return SyncCommandExecution.Read<SqliteException, LongSyncResult>(
            connection: connection,
            setSql: command =>
                command.CommandText = "SELECT MIN(last_synced_version) FROM _sync_mapping_state",
            parameters: () => [],
            read: command =>
            {
                var result = command.ExecuteScalar();

                if (result is null || result == DBNull.Value)
                {
                    return new LongSyncOk(0);
                }

                return new LongSyncOk(Convert.ToInt64(result, CultureInfo.InvariantCulture));
            },
            failure: ex =>
            {
                return new LongSyncError(
                    new SyncErrorDatabase($"Failed to get min synced version: {ex.Message}")
                );
            }
        );
    }

    internal static MappingStateEntry ReadMappingStateEntry(
        global::System.Data.Common.DbDataReader reader
    ) => new(reader.GetString(0), reader.GetInt64(1), reader.GetString(2), reader.GetInt64(3));

    // Implements [SYNC-REPOSITORY-SHARED-WRITES].
    internal static BoolSyncResult WriteMappingState(
        SqliteConnection connection,
        MappingStateEntry state,
        Action<global::System.Data.Common.DbCommand> setSql
    ) =>
        WriteUpsert(
            connection: connection,
            setSql: setSql,
            parameters:
            [
                ("@mappingId", state.MappingId),
                ("@version", state.LastSyncedVersion),
                ("@timestamp", state.LastSyncTimestamp),
                ("@records", state.RecordsSynced),
            ],
            failurePrefix: "Failed to upsert mapping state"
        );

    // Implements [SYNC-REPOSITORY-SHARED-WRITES].
    internal static BoolSyncResult WriteRecordHash(
        SqliteConnection connection,
        RecordHashEntry entry,
        Action<global::System.Data.Common.DbCommand> setSql
    ) =>
        WriteUpsert(
            connection: connection,
            setSql: setSql,
            parameters:
            [
                ("@mappingId", entry.MappingId),
                ("@sourcePk", entry.SourcePk),
                ("@hash", entry.PayloadHash),
                ("@syncedAt", entry.SyncedAt),
            ],
            failurePrefix: "Failed to upsert record hash"
        );

    private static BoolSyncResult WriteUpsert(
        SqliteConnection connection,
        Action<global::System.Data.Common.DbCommand> setSql,
        (string Name, object Value)[] parameters,
        string failurePrefix
    ) =>
        SyncCommandExecution.Write<SqliteException>(
            connection: connection,
            setSql: setSql,
            parameters: () => [.. parameters],
            failurePrefix: failurePrefix
        );
}
