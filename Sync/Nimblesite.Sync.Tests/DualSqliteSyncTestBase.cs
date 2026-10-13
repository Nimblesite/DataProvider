using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Tests.Shared;

namespace Nimblesite.Sync.Tests;

// Implements [CI-TEST-SHARED-SCENARIOS].
/// <summary>
/// Shared dual-SQLite database fixture for sync integration tests.
/// Creates a server and client database with sync schema and disposes both.
/// </summary>
public abstract class DualSqliteSyncTestBase : IDisposable
{
    private protected static readonly ILogger Logger = NullLogger.Instance;

    private protected readonly string _serverDbPath;
    private protected readonly string _clientDbPath;
    private protected readonly SqliteConnection _serverDb;
    private protected readonly SqliteConnection _clientDb;

    /// <summary>
    /// Opens the server and client databases, applying the supplied schema SQL.
    /// </summary>
    /// <param name="serverPath">Path to the server database file.</param>
    /// <param name="clientPath">Path to the client database file.</param>
    /// <param name="serverOrigin">Origin ID for the server database.</param>
    /// <param name="clientOrigin">Origin ID for the client database.</param>
    /// <param name="createSql">Factory producing the schema SQL for an origin.</param>
    private protected DualSqliteSyncTestBase(
        string serverPath,
        string clientPath,
        string serverOrigin,
        string clientOrigin,
        Func<string, string> createSql
    )
    {
        _serverDbPath = serverPath;
        _clientDbPath = clientPath;
        _serverDb = CreateSyncDatabase(
            originId: serverOrigin,
            dbPath: _serverDbPath,
            createSql: createSql
        );
        _clientDb = CreateSyncDatabase(
            originId: clientOrigin,
            dbPath: _clientDbPath,
            createSql: createSql
        );
    }

    private static SqliteConnection CreateSyncDatabase(
        string originId,
        string dbPath,
        Func<string, string> createSql
    ) =>
        SyncIntegrationTests.VerifyReusableWorkflow10(
            configure0: command => command.CommandText = createSql(arg: originId),
            input0: dbPath
        );

    /// <summary>
    /// Disposes both databases and deletes their backing files.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the databases owned by this fixture.
    /// </summary>
    /// <param name="disposing">True when disposal is driven by the caller.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _serverDb.Dispose();
            _clientDb.Dispose();
            TempSqliteDatabase.DeleteIfExists(path: _serverDbPath);
            TempSqliteDatabase.DeleteIfExists(path: _clientDbPath);
        }
    }
}
