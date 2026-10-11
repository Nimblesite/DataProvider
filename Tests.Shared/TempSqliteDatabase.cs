namespace Tests.Shared;

// Implements [CI-TEST-SHARED-TEMP-SQLITE]. One isolated on-disk SQLite database
// per fixture, with the temp file path retained for cleanup and DDL assertions.
internal static partial class TempSqliteDatabase
{
    // Implements [CI-TEST-SHARED-TEMP-CLEANUP].
    internal static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception exception)
                when (exception
                        is IOException
                            or UnauthorizedAccessException
                            or ArgumentException
                            or NotSupportedException
                            or System.Security.SecurityException
                )
            {
                // Preserve existing teardown behavior when a file remains locked.
            }
        }
    }

    internal static string PathFor(string prefix) =>
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{prefix}_{Guid.NewGuid():N}.db");

    internal static Microsoft.Data.Sqlite.SqliteConnection Open(string path)
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path}");
        connection.Open();
        return connection;
    }

    internal static (string Path, Microsoft.Data.Sqlite.SqliteConnection Connection) Create(
        string prefix
    )
    {
        var path = PathFor(prefix);
        return (path, Open(path));
    }

    internal static (string Path, Microsoft.Data.Sqlite.SqliteConnection Connection) CreateClosed(
        string prefix
    )
    {
        var path = PathFor(prefix);
        return (path, new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path}"));
    }
}
