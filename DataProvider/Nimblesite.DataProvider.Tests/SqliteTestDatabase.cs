using Microsoft.Data.Sqlite;

namespace Nimblesite.DataProvider.Tests;

/// <summary>
/// Shared file-based SQLite database for E2E test fixtures: opens a temp-file
/// connection on construction and deletes the file on dispose.
/// </summary>
internal sealed class SqliteTestDatabase : IDisposable
{
    private readonly string _dbPath;

    /// <summary>
    /// The open connection to the temp-file SQLite database.
    /// </summary>
    public SqliteConnection Connection { get; }

    /// <summary>
    /// Creates and opens a temp-file SQLite database.
    /// </summary>
    /// <param name="prefix">Temp-file name prefix identifying the fixture.</param>
    public SqliteTestDatabase(string prefix)
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"{prefix}_{Guid.NewGuid()}.db");
        Connection = new SqliteConnection($"Data Source={_dbPath}");
        Connection.Open();
    }

    /// <summary>
    /// Disposes the connection and best-effort deletes the temp file.
    /// </summary>
    public void Dispose()
    {
        Connection.Dispose();
        try
        {
            File.Delete(_dbPath);
        }
        catch (IOException)
        { /* cleanup best-effort */
        }
    }
}
