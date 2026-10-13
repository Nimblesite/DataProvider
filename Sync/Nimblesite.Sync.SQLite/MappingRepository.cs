using System.Globalization;
using Microsoft.Data.Sqlite;
using static Nimblesite.Sync.SQLite.SqliteCommandExecution;

namespace Nimblesite.Sync.SQLite;

// Implements [SYNC-SCALAR-READ-SHARED].

/// <summary>
/// SQLite repository for sync mapping state and record hashes.
/// Implements spec Section 7.5.2 tables.
/// </summary>
public static class MappingRepository
{
    /// <summary>
    /// Gets mapping state by ID.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="mappingId">Mapping identifier.</param>
    /// <returns>Mapping state or null if not found.</returns>
    public static MappingStateResult GetMappingState(
        SqliteConnection connection,
        string mappingId
    ) => MappingStateRepository.GetMappingState(connection: connection, mappingId: mappingId);

    /// <summary>
    /// Gets all mapping states.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <returns>List of mapping states.</returns>
    public static MappingStateListResult GetAllMappingStates(SqliteConnection connection) =>
        Execute<MappingStateListResult>(
            connection,
            cmd =>
                cmd.CommandText = global::Nimblesite
                    .Sync
                    .SQLite
                    .MappingStateRepository
                    .SqlSpecification2,
            [],
            cmd =>
            {
                return new MappingStateListOk(
                    SyncCommandExecution.ReadRows<MappingStateEntry>(
                        command: cmd,
                        read: row => MappingStateRepository.ReadMappingStateEntry(reader: row)
                    )
                );
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
        MappingStateRepository.WriteMappingState(
            connection: connection,
            state: state,
            setSql: command => command.CommandText = MappingStateRepository.SqlSpecification25
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
    ) => MappingStateRepository.DeleteMappingState(connection: connection, mappingId: mappingId);

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
        MappingStateRepository.GetRecordHash(
            connection: connection,
            mappingId: mappingId,
            sourcePk: sourcePk
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
        MappingStateRepository.WriteRecordHash(
            connection: connection,
            entry: hash,
            setSql: command => command.CommandText = MappingStateRepository.SqlSpecification26
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
                cmd.CommandText = global::Nimblesite
                    .Sync
                    .SQLite
                    .MappingStateRepository
                    .SqlSpecification27,
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
        MappingStateRepository.DeleteRecordHashesForMapping(
            connection: connection,
            mappingId: mappingId
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
