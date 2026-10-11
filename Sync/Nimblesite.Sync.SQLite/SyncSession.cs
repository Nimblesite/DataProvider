using Microsoft.Data.Sqlite;

namespace Nimblesite.Sync.SQLite;

/// <summary>
/// Manages sync session state for trigger suppression in SQLite.
/// Implements spec Section 8 (Trigger Suppression).
/// </summary>
public static class SyncSessionManager
{
    /// <summary>
    /// Enables trigger suppression. Call before applying incoming changes.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <returns>Success or database error.</returns>
    public static BoolSyncResult EnableSuppression(SqliteConnection connection) =>
        SyncCommandExecution.Write<SqliteException>(
            connection: connection,
            setSql: command => command.CommandText = "UPDATE _sync_session SET sync_active = 1",
            parameters: () => [],
            failurePrefix: "Failed to enable trigger suppression"
        );

    /// <summary>
    /// Disables trigger suppression. Call after sync completes.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <returns>Success or database error.</returns>
    public static BoolSyncResult DisableSuppression(SqliteConnection connection) =>
        SyncCommandExecution.Write<SqliteException>(
            connection: connection,
            setSql: command => command.CommandText = "UPDATE _sync_session SET sync_active = 0",
            parameters: () => [],
            failurePrefix: "Failed to disable trigger suppression"
        );

    /// <summary>
    /// Checks if trigger suppression is currently active.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <returns>True if suppression active, or database error.</returns>
    public static BoolSyncResult IsSuppressionActive(SqliteConnection connection)
    {
        return SyncCommandExecution.Read<SqliteException, BoolSyncResult>(
            connection: connection,
            setSql: command =>
                command.CommandText = "SELECT sync_active FROM _sync_session LIMIT 1",
            parameters: () => [],
            read: command =>
            {
                var result = command.ExecuteScalar();
                return new BoolSyncOk(result is long value && value == 1);
            },
            failure: ex =>
            {
                return new BoolSyncError(
                    new SyncErrorDatabase($"Failed to check suppression state: {ex.Message}")
                );
            }
        );
    }
}
