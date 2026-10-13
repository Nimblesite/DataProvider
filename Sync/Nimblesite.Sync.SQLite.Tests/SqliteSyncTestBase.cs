namespace Nimblesite.Sync.SQLite.Tests;

// Implements [CI-TEST-SHARED-SCENARIOS].
/// <summary>
/// Shared single-SQLite-database fixture for spec compliance tests.
/// Opens a temp sync database, applies the supplied schema, and cleans it up.
/// </summary>
public abstract class SqliteSyncTestBase : IDisposable
{
    private protected readonly SqliteConnection _db;
    private protected readonly string _dbPath;
    private protected readonly string _originId;

    /// <summary>
    /// Opens the temp database and applies the supplied schema.
    /// </summary>
    /// <param name="name">Base file name for the temp database.</param>
    /// <param name="createSchema">Applies the spec table schema to the open database.</param>
    private protected SqliteSyncTestBase(string name, Action<SqliteConnection> createSchema)
    {
        _dbPath = TempSqliteDatabase.PathFor(name);
        _originId = Guid.NewGuid().ToString();
        _db = TempSqliteDatabase.OpenSync(path: _dbPath, origin: _originId);
        createSchema(_db);
    }

    /// <summary>
    /// Disposes the database and deletes its backing file.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the database owned by this fixture.
    /// </summary>
    /// <param name="disposing">True when disposal is driven by the caller.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            EndToEndSyncTests.VerifyReusableWorkflow5(input0: _db, input1: _dbPath);
        }
    }
}
