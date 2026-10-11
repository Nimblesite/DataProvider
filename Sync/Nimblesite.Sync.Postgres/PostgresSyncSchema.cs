namespace Nimblesite.Sync.Postgres;

/// <summary>
/// Schema management for PostgreSQL sync tables.
/// Implements spec Appendix A (Complete Schema) for Postgres.
/// </summary>
public static class PostgresSyncSchema
{
    /// <summary>
    /// Creates the sync schema tables in PostgreSQL.
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <returns>Success or database error.</returns>
    public static BoolSyncResult CreateSchema(NpgsqlConnection connection) =>
        SyncCommandExecution.Write<NpgsqlException>(
            connection: connection,
            setSql: command =>
                command.CommandText = """
                -- Sync state (persistent)
                CREATE TABLE IF NOT EXISTS _sync_state (
                    key TEXT PRIMARY KEY,
                    value TEXT NOT NULL
                );

                -- Sync session (ephemeral flag)
                CREATE TABLE IF NOT EXISTS _sync_session (
                    sync_active INTEGER DEFAULT 0
                );

                -- Change log
                CREATE TABLE IF NOT EXISTS _sync_log (
                    version BIGSERIAL PRIMARY KEY,
                    table_name TEXT NOT NULL,
                    pk_value TEXT NOT NULL,
                    operation TEXT NOT NULL CHECK (operation IN ('insert', 'update', 'delete')),
                    payload TEXT,
                    origin TEXT NOT NULL,
                    timestamp TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_sync_log_version ON _sync_log(version);
                CREATE INDEX IF NOT EXISTS idx_sync_log_table ON _sync_log(table_name, version);

                -- Client tracking
                CREATE TABLE IF NOT EXISTS _sync_clients (
                    origin_id TEXT PRIMARY KEY,
                    last_sync_version BIGINT NOT NULL DEFAULT 0,
                    last_sync_timestamp TEXT NOT NULL,
                    created_at TEXT NOT NULL
                );

                -- Subscriptions
                CREATE TABLE IF NOT EXISTS _sync_subscriptions (
                    subscription_id TEXT PRIMARY KEY,
                    origin_id TEXT NOT NULL,
                    subscription_type TEXT NOT NULL,
                    table_name TEXT NOT NULL,
                    filter TEXT,
                    created_at TEXT NOT NULL,
                    expires_at TEXT
                );

                CREATE INDEX IF NOT EXISTS idx_subs_table ON _sync_subscriptions(table_name);
                CREATE INDEX IF NOT EXISTS idx_subs_origin ON _sync_subscriptions(origin_id);

                -- Mapping sync state (Section 7.5.2)
                CREATE TABLE IF NOT EXISTS _sync_mapping_state (
                    mapping_id TEXT PRIMARY KEY,
                    last_synced_version BIGINT NOT NULL DEFAULT 0,
                    last_sync_timestamp TEXT NOT NULL,
                    records_synced BIGINT NOT NULL DEFAULT 0
                );

                -- Record hash tracking (Section 7.5.2)
                CREATE TABLE IF NOT EXISTS _sync_record_hashes (
                    mapping_id TEXT NOT NULL,
                    source_pk TEXT NOT NULL,
                    payload_hash TEXT NOT NULL,
                    synced_at TEXT NOT NULL,
                    PRIMARY KEY (mapping_id, source_pk)
                );

                CREATE INDEX IF NOT EXISTS idx_record_hashes_mapping ON _sync_record_hashes(mapping_id);

                -- Initialize session
                INSERT INTO _sync_session VALUES (0) ON CONFLICT DO NOTHING;

                -- Initialize state
                INSERT INTO _sync_state (key, value) VALUES ('origin_id', '') ON CONFLICT DO NOTHING;
                INSERT INTO _sync_state (key, value) VALUES ('last_server_version', '0') ON CONFLICT DO NOTHING;
                INSERT INTO _sync_state (key, value) VALUES ('last_push_version', '0') ON CONFLICT DO NOTHING;
                """,
            parameters: () => [],
            failurePrefix: "Failed to create schema"
        );

    /// <summary>
    /// Gets the origin ID from _sync_state.
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <returns>Origin ID or database error.</returns>
    public static StringSyncResult GetOriginId(NpgsqlConnection connection)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT value FROM _sync_state WHERE key = 'origin_id'";
            var result = cmd.ExecuteScalar();
            return new StringSyncOk(result?.ToString() ?? "");
        }
        catch (NpgsqlException ex)
        {
            return new StringSyncError(
                new SyncErrorDatabase($"Failed to get origin ID: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Sets the origin ID in _sync_state.
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <param name="originId">Origin ID to set.</param>
    /// <returns>Success or database error.</returns>
    public static BoolSyncResult SetOriginId(NpgsqlConnection connection, string originId) =>
        SyncCommandExecution.Write<NpgsqlException>(
            connection: connection,
            setSql: command =>
                command.CommandText = """
                INSERT INTO _sync_state (key, value) VALUES ('origin_id', @originId)
                ON CONFLICT (key) DO UPDATE SET value = @originId
                """,
            parameters: () => [("@originId", originId)],
            failurePrefix: "Failed to set origin ID"
        );

    /// <summary>
    /// Initializes origin ID if not set.
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <returns>Origin ID (existing or new).</returns>
    public static string InitializeOriginId(NpgsqlConnection connection)
    {
        var result = GetOriginId(connection);
        if (result is StringSyncOk ok && !string.IsNullOrEmpty(ok.Value))
        {
            return ok.Value;
        }

        var newOriginId = Guid.NewGuid().ToString();
        _ = SetOriginId(connection, newOriginId);
        return newOriginId;
    }
}
