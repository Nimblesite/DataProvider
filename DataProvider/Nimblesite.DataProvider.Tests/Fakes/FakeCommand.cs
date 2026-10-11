using System.Data;

#pragma warning disable CA1515 // Make types internal
#pragma warning disable CS8765 // Nullability of parameter doesn't match overridden member

namespace Nimblesite.DataProvider.Tests.Fakes;

/// <summary>
/// Fake database command for testing
/// </summary>
public sealed class FakeCommand : DbCommand
{
    private readonly Func<string, DbDataReader> _dataReaderFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeCommand"/> class
    /// </summary>
    /// <param name="connection">The fake database connection</param>
    /// <param name="dataReaderFactory">Factory function that returns a DataReader based on the SQL statement</param>
    public FakeCommand(DbConnection connection, Func<string, DbDataReader> dataReaderFactory)
    {
        DbConnection = connection ?? throw new ArgumentNullException(nameof(connection));
        _dataReaderFactory =
            dataReaderFactory ?? throw new ArgumentNullException(nameof(dataReaderFactory));
    }

    public override string CommandText { get; set; } = string.Empty;

    public override int CommandTimeout { get; set; } = 30;

    public override CommandType CommandType { get; set; } = CommandType.Text;

    public override bool DesignTimeVisible { get; set; }

    public override UpdateRowSource UpdatedRowSource { get; set; }

    protected override DbConnection? DbConnection { get; set; }

    protected override DbParameterCollection DbParameterCollection => new FakeParameterCollection();

    protected override DbTransaction? DbTransaction { get; set; }

    public override void Cancel()
    {
        // No-op for fake command
    }

    public override int ExecuteNonQuery() => 1; // Fake success

    public override object? ExecuteScalar()
    {
        using var reader = ExecuteReader();
        return reader.Read() ? reader.GetValue(0) : null;
    }

    public override void Prepare()
    {
        // No-op for fake command
    }

    protected override DbParameter CreateDbParameter() => new FakeParameter();

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) =>
        _dataReaderFactory(CommandText);

    protected override Task<DbDataReader> ExecuteDbDataReaderAsync(
        CommandBehavior behavior,
        CancellationToken cancellationToken
    ) => Task.FromResult(ExecuteDbDataReader(behavior));

    public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) =>
        Task.FromResult(ExecuteNonQuery());

    public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken) =>
        Task.FromResult(ExecuteScalar());
}
