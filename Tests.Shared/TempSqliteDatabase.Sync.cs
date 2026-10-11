namespace Tests.Shared;

internal static partial class TempSqliteDatabase
{
    // Implements [SYNC-TEST-SHARED-DATABASE-BOOTSTRAP].
    internal static Microsoft.Data.Sqlite.SqliteConnection OpenSync(string path, string origin)
    {
        var connection = Open(path: path);
        InitializeSync(connection: connection, origin: origin);
        return connection;
    }

    internal static void InitializeSync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        string origin
    )
    {
        Nimblesite.Sync.SQLite.SyncSchema.CreateSchema(connection: connection);
        Nimblesite.Sync.SQLite.SyncSchema.SetOriginId(connection: connection, originId: origin);
    }
}
