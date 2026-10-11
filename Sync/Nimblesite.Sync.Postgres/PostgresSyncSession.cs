namespace Nimblesite.Sync.Postgres;

/// <summary>
/// Manages sync session state for PostgreSQL.
/// Implements spec Section 8 (Trigger Suppression) for Postgres.
/// </summary>
public static class PostgresSyncSession
{
    /// <summary>
    /// Enables suppression (sets sync_active = 1).
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <returns>Success or database error.</returns>
    public static BoolSyncResult EnableSuppression(NpgsqlConnection connection) =>
        SyncCommandExecution.Write<NpgsqlException>(
            connection: connection,
            setSql: command => command.CommandText = "UPDATE _sync_session SET sync_active = 1",
            parameters: () => [],
            failurePrefix: "Failed to enable suppression"
        );

    /// <summary>
    /// Disables suppression (sets sync_active = 0).
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <returns>Success or database error.</returns>
    public static BoolSyncResult DisableSuppression(NpgsqlConnection connection) =>
        SyncCommandExecution.Write<NpgsqlException>(
            connection: connection,
            setSql: command => command.CommandText = "UPDATE _sync_session SET sync_active = 0",
            parameters: () => [],
            failurePrefix: "Failed to disable suppression"
        );
}
