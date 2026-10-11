using System.Data;

#pragma warning disable CA1515 // Make types internal
#pragma warning disable CA1513 // Use ObjectDisposedException.ThrowIf

namespace Nimblesite.DataProvider.Tests.Fakes;

/// <summary>
/// Fake database transaction for testing with injectable data callback
/// </summary>
public sealed class FakeTransaction : DbTransaction
{
    private readonly Func<string, DbDataReader> _dataReaderFactory;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeTransaction"/> class
    /// </summary>
    /// <param name="connection">The fake database connection</param>
    /// <param name="dataReaderFactory">Callback function that returns data based on select statement using switch expression</param>
    public FakeTransaction(DbConnection connection, Func<string, DbDataReader> dataReaderFactory)
    {
        DbConnection = connection ?? throw new ArgumentNullException(nameof(connection));
        _dataReaderFactory =
            dataReaderFactory ?? throw new ArgumentNullException(nameof(dataReaderFactory));
    }

    public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

    protected override DbConnection DbConnection { get; }

    /// <summary>
    /// Gets a data reader for the specified SQL statement using the injected callback
    /// </summary>
    /// <param name="sql">The SQL statement to execute</param>
    /// <returns>A data reader with the appropriate fake data</returns>
    public DbDataReader GetDataReader(string sql)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(FakeTransaction));

        return _dataReaderFactory(sql);
    }

    public override void Commit()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(FakeTransaction));

        // No-op for fake transaction
    }

    public override void Rollback()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(FakeTransaction));

        // No-op for fake transaction
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_isDisposed)
        {
            _isDisposed = true;
        }
        base.Dispose(disposing);
    }
}
