using System.Data;

#pragma warning disable CA1515 // Make types internal
#pragma warning disable CA2000 // Dispose objects before losing scope
#pragma warning disable CA1849 // Synchronous blocking calls
#pragma warning disable CS8765 // Nullability of parameter doesn't match overridden member

namespace Nimblesite.DataProvider.Tests.Fakes;

/// <summary>
/// Fake database connection for testing that acts as a factory for FakeTransaction
/// </summary>
public sealed class FakeDbConnection : DbConnection
{
    private readonly Func<string, DbDataReader> _dataReaderFactory;
    private ConnectionState _state = ConnectionState.Closed;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeDbConnection"/> class
    /// </summary>
    /// <param name="dataReaderFactory">Factory function that returns a DataReader based on the SQL statement</param>
    public FakeDbConnection(Func<string, DbDataReader> dataReaderFactory)
    {
        _dataReaderFactory =
            dataReaderFactory ?? throw new ArgumentNullException(nameof(dataReaderFactory));
    }

    public override string ConnectionString { get; set; } = "FakeConnection";

    public override string Database => "FakeDatabase";

    public override string DataSource => "FakeDataSource";

    public override string ServerVersion => "1.0.0";

    public override ConnectionState State => _state;

    public override void ChangeDatabase(string databaseName)
    {
        // No-op for fake connection
    }

    public override void Close() => _state = ConnectionState.Closed;

    public override void Open() => _state = ConnectionState.Open;

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
    {
        if (_state != ConnectionState.Open)
            throw new InvalidOperationException("Connection must be open to begin a transaction");

        return new FakeTransaction(this, _dataReaderFactory);
    }

    protected override DbCommand CreateDbCommand() => new FakeCommand(this, _dataReaderFactory);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Close();
        }
        base.Dispose(disposing);
    }
}
