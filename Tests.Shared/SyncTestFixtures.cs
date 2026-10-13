using Tests.Shared;

namespace Nimblesite.TestSupport;

// Implements [SYNC-TEST-SHARED-SCENARIOS].
/// <summary>
/// Shared PostgreSQL-backed sync test fixture. Owns the per-assembly container
/// database connection string, the tracked temporary SQLite databases, and the
/// lifetime hooks, so cross-database sync test classes share one implementation.
/// </summary>
public abstract class PostgresSyncTestFixture : IAsyncLifetime
{
    private protected readonly ILogger _logger = NullLogger.Instance;
    private protected readonly List<string> _sqliteDbPaths = [];
    private protected string _postgresConnectionString = null!;

    private readonly PostgresContainerFixture _fixture;
    private readonly string _databaseName;

    private protected PostgresSyncTestFixture(PostgresContainerFixture fixture, string databaseName)
    {
        _fixture = fixture;
        _databaseName = databaseName;
    }

    /// <summary>Creates the per-test PostgreSQL database inside the shared container.</summary>
    public async Task InitializeAsync() =>
        _postgresConnectionString = await _fixture
            .CreateDatabaseConnectionStringAsync(_databaseName)
            .ConfigureAwait(false);

    /// <summary>Deletes every temporary SQLite database created by the fixture.</summary>
    public Task DisposeAsync()
    {
        foreach (var dbPath in _sqliteDbPaths)
            TempSqliteDatabase.DeleteIfExists(path: dbPath);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Opens a PostgreSQL database with the sync schema, origin id and the supplied
    /// table DDL, preserving the original failure messages and result checks.
    /// </summary>
    private protected Npgsql.NpgsqlConnection CreatePostgresSyncDatabase(
        string originId,
        string createTableSql
    )
    {
        var conn = new Npgsql.NpgsqlConnection(_postgresConnectionString);
        conn.Open();

        var schemaResult = Nimblesite.Sync.Postgres.PostgresSyncSchema.CreateSchema(conn);
        if (!Succeeded(result: schemaResult))
        {
            throw new InvalidOperationException(
                $"Failed to create Postgres schema: {schemaResult}"
            );
        }

        var originResult = Nimblesite.Sync.Postgres.PostgresSyncSchema.SetOriginId(conn, originId);
        if (!Succeeded(result: originResult))
        {
            throw new InvalidOperationException($"Failed to set origin ID: {originResult}");
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = createTableSql;
        cmd.ExecuteNonQuery();

        return conn;
    }

    private static bool Succeeded(Outcome.Result<bool, Nimblesite.Sync.Core.SyncError> result) =>
        result
        is Outcome.Result<bool, Nimblesite.Sync.Core.SyncError>.Ok<
            bool,
            Nimblesite.Sync.Core.SyncError
        >;
}

// Implements [SYNC-TEST-SHARED-SCENARIOS].
/// <summary>
/// Shared dual-SQLite HTTP test fixture. Creates a server and a client file
/// database through the supplied initializer and disposes both plus their files.
/// </summary>
public abstract class HttpSqliteSyncTestFixture : IDisposable
{
    private protected readonly string _serverDbPath;
    private protected readonly string _clientDbPath;
    private protected readonly SqliteConnection _serverConn;
    private protected readonly SqliteConnection _clientConn;
    private protected readonly string _serverOriginId = Guid.NewGuid().ToString();
    private protected readonly string _clientOriginId = Guid.NewGuid().ToString();
    private protected readonly ILogger _logger = NullLogger.Instance;

    private protected HttpSqliteSyncTestFixture(
        string serverName,
        string clientName,
        Action<SqliteConnection, string> initialize
    )
    {
        (_serverDbPath, _serverConn) = TempSqliteDatabase.Create(serverName);
        initialize(_serverConn, _serverOriginId);
        (_clientDbPath, _clientConn) = TempSqliteDatabase.Create(clientName);
        initialize(_clientConn, _clientOriginId);
    }

    /// <summary>Disposes the databases owned by this fixture.</summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases the databases and deletes their backing files.</summary>
    /// <param name="disposing">True when disposal is driven by the caller.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
            return;

        _serverConn.Close();
        _serverConn.Dispose();
        _clientConn.Close();
        _clientConn.Dispose();

        try
        {
            if (File.Exists(_serverDbPath))
                File.Delete(_serverDbPath);
            if (File.Exists(_clientDbPath))
                File.Delete(_clientDbPath);
        }
        catch
        {
            // Ignore cleanup errors, matching the original helper.
        }
    }
}
