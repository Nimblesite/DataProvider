namespace Nimblesite.Sync.SQLite.Tests;

public sealed partial class SqliteExtensionIntegrationTests
{
    private readonly SqliteConnection _db;
    private readonly string _dbPath = global::Tests.Shared.TempSqliteDatabase.PathFor(
        "sqliteextensionintegrationtests"
    );
    private readonly string _originId = Guid.NewGuid().ToString();
    private const string Timestamp = "2025-01-01T00:00:00.000Z";

    public SqliteExtensionIntegrationTests()
    {
        _db = global::Tests.Shared.TempSqliteDatabase.OpenSync(path: _dbPath, origin: _originId);
    }

    public void Dispose()
    {
        global::Nimblesite.Sync.SQLite.Tests.EndToEndSyncTests.VerifyReusableWorkflow5(
            input0: _db,
            input1: _dbPath
        );
    }
}
