using Microsoft.Data.Sqlite;

namespace Nimblesite.Sync.SQLite.Tests;

public sealed partial class SqliteExtensionIntegrationTests
{
    private readonly SqliteConnection _db;
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(),
        $"sqliteextensionintegrationtests_{Guid.NewGuid()}.db"
    );
    private readonly string _originId = Guid.NewGuid().ToString();
    private const string Timestamp = "2025-01-01T00:00:00.000Z";

    public SqliteExtensionIntegrationTests()
    {
        _db = new SqliteConnection($"Data Source={_dbPath}");
        _db.Open();
        SyncSchema.CreateSchema(_db);
        SyncSchema.SetOriginId(_db, _originId);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_dbPath))
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch
            {
                /* File may be locked */
            }
        }
    }
}
