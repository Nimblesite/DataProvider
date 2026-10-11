// Implements [CI-TEST-SHARED-COMMAND].
using Microsoft.Data.Sqlite;
using Outcome;
using Tests.Shared;

namespace Nimblesite.DataProvider.Tests;

/// <summary>
/// E2E tests: transaction workflows with real SQLite databases.
/// Tests commit, rollback, multi-table transactions, and DbTransact helpers.
/// </summary>
public sealed partial class SqliteTransactionE2ETests : IDisposable
{
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(),
        $"tx_e2e_{Guid.NewGuid()}.db"
    );

    private readonly SqliteConnection _connection;

    public SqliteTransactionE2ETests()
    {
        _connection = new SqliteConnection($"Data Source={_dbPath}");
        _connection.Open();
        CreateSchema();
    }

    public void Dispose()
    {
        _connection.Dispose();
        try
        {
            File.Delete(_dbPath);
        }
#pragma warning disable CA1031 // Cleanup is best-effort
        catch (IOException)
        {
            // File may be locked by another process
        }
        catch (UnauthorizedAccessException)
        {
            // May not have permission
        }
#pragma warning restore CA1031
    }

    private void CreateSchema()
    {
        DatabaseCommands.Execute(
            connection: _connection,
            configure: command => command.CommandText = Data_CreateSchema_1
        );

        // Seed inventory
        DatabaseCommands.Execute(
            connection: _connection,
            configure: command => command.CommandText = Data_CreateSchema_2
        );
    }

    private static void AssertLongQueryValue(
        Result<IReadOnlyList<long>, SqlError> result,
        long expected
    )
    {
        Assert.True(
            result is Result<IReadOnlyList<long>, SqlError>.Ok<IReadOnlyList<long>, SqlError>
        );
        if (result is Result<IReadOnlyList<long>, SqlError>.Ok<IReadOnlyList<long>, SqlError> ok)
        {
            Assert.Equal(expected, ok.Value[0]);
        }
    }

    [Fact]
    public void TransactionCommit_MultiTableInsert_AllDataPersisted()
    {
        using var tx = _connection.BeginTransaction();

        // Insert order within transaction
        var orderId = Guid.NewGuid().ToString();
        var orderInsert = tx.Execute(
            sql: "INSERT INTO Orders (Id, CustomerId, Total, Status) VALUES (@id, @cid, @total, @status)",
            parameters:
            [
                new SqliteParameter("@id", orderId),
                new SqliteParameter("@cid", "CUST-001"),
                new SqliteParameter("@total", 150.75),
                new SqliteParameter("@status", "confirmed"),
            ]
        );
        Assert.True(orderInsert is IntOk);
        if (orderInsert is IntOk insertOk)
        {
            Assert.Equal(1, insertOk.Value);
        }

        // Insert order items within same transaction
        var item1Id = Guid.NewGuid().ToString();
        var item2Id = Guid.NewGuid().ToString();
        tx.Execute(
            sql: "INSERT INTO OrderItems (Id, OrderId, ProductName, Quantity, UnitPrice) VALUES (@id, @oid, @name, @qty, @price)",
            parameters:
            [
                new SqliteParameter("@id", item1Id),
                new SqliteParameter("@oid", orderId),
                new SqliteParameter("@name", "Widget"),
                new SqliteParameter("@qty", 3),
                new SqliteParameter("@price", 25.25),
            ]
        );
        tx.Execute(
            sql: "INSERT INTO OrderItems (Id, OrderId, ProductName, Quantity, UnitPrice) VALUES (@id, @oid, @name, @qty, @price)",
            parameters:
            [
                new SqliteParameter("@id", item2Id),
                new SqliteParameter("@oid", orderId),
                new SqliteParameter("@name", "Gadget"),
                new SqliteParameter("@qty", 2),
                new SqliteParameter("@price", 37.50),
            ]
        );

        // Update inventory within transaction
        tx.Execute(
            sql: "UPDATE Inventory SET StockCount = StockCount - @qty WHERE ProductName = @name",
            parameters: [new SqliteParameter("@qty", 3), new SqliteParameter("@name", "Widget")]
        );
        tx.Execute(
            sql: "UPDATE Inventory SET StockCount = StockCount - @qty WHERE ProductName = @name",
            parameters: [new SqliteParameter("@qty", 2), new SqliteParameter("@name", "Gadget")]
        );

        // Query within transaction to verify item count
        var itemCount = tx.Query<long>(
            sql: "SELECT COUNT(*) FROM OrderItems WHERE OrderId = @oid",
            parameters: [new SqliteParameter("@oid", orderId)],
            mapper: r => r.GetInt64(0)
        );
        AssertLongQueryValue(itemCount, expected: 2L);

        // Commit
        tx.Commit();

        // Verify data persisted after commit
        AssertLongQueryValue(
            _connection.Query<long>(sql: "SELECT COUNT(*) FROM Orders", mapper: r => r.GetInt64(0)),
            expected: 1L
        );

        AssertLongQueryValue(
            _connection.Query<long>(
                sql: "SELECT COUNT(*) FROM OrderItems",
                mapper: r => r.GetInt64(0)
            ),
            expected: 2L
        );

        AssertLongQueryValue(
            _connection.Query<long>(
                sql: "SELECT StockCount FROM Inventory WHERE ProductName = 'Widget'",
                mapper: r => r.GetInt64(0)
            ),
            expected: 97L
        );

        AssertLongQueryValue(
            _connection.Query<long>(
                sql: "SELECT StockCount FROM Inventory WHERE ProductName = 'Gadget'",
                mapper: r => r.GetInt64(0)
            ),
            expected: 48L
        );
    }

    [Fact]
    public void TransactionRollback_FailedOperation_NoDataPersisted()
    {
        // Verify initial state
        AssertLongQueryValue(
            _connection.Query<long>(sql: "SELECT COUNT(*) FROM Orders", mapper: r => r.GetInt64(0)),
            expected: 0L
        );

        using var tx = _connection.BeginTransaction();

        // Insert order
        var orderId = Guid.NewGuid().ToString();
        tx.Execute(
            sql: "INSERT INTO Orders (Id, CustomerId, Total) VALUES (@id, @cid, @total)",
            parameters:
            [
                new SqliteParameter("@id", orderId),
                new SqliteParameter("@cid", "CUST-002"),
                new SqliteParameter("@total", 99.99),
            ]
        );

        // Insert items
        tx.Execute(
            sql: "INSERT INTO OrderItems (Id, OrderId, ProductName, Quantity, UnitPrice) VALUES (@id, @oid, @name, @qty, @price)",
            parameters:
            [
                new SqliteParameter("@id", Guid.NewGuid().ToString()),
                new SqliteParameter("@oid", orderId),
                new SqliteParameter("@name", "Widget"),
                new SqliteParameter("@qty", 5),
                new SqliteParameter("@price", 19.99),
            ]
        );

        // Verify data exists within transaction
        AssertLongQueryValue(
            tx.Query<long>(sql: "SELECT COUNT(*) FROM Orders", mapper: r => r.GetInt64(0)),
            expected: 1L
        );

        // Rollback
        tx.Rollback();

        // Verify NO data persisted
        AssertLongQueryValue(
            _connection.Query<long>(sql: "SELECT COUNT(*) FROM Orders", mapper: r => r.GetInt64(0)),
            expected: 0L
        );

        AssertLongQueryValue(
            _connection.Query<long>(
                sql: "SELECT COUNT(*) FROM OrderItems",
                mapper: r => r.GetInt64(0)
            ),
            expected: 0L
        );

        // Verify inventory unchanged
        AssertLongQueryValue(
            _connection.Query<long>(
                sql: "SELECT StockCount FROM Inventory WHERE ProductName = 'Widget'",
                mapper: r => r.GetInt64(0)
            ),
            expected: 100L
        );
    }

    [Fact]
    public void TransactionQueryWorkflow_QueryAndModifyInTransaction_ConsistentReads()
    {
        // Seed some orders first (outside transaction)
        for (int i = 0; i < 5; i++)
        {
            _connection.Execute(
                sql: "INSERT INTO Orders (Id, CustomerId, Total, Status) VALUES (@id, @cid, @total, @status)",
                parameters:
                [
                    new SqliteParameter("@id", Guid.NewGuid().ToString()),
                    new SqliteParameter("@cid", $"CUST-{i:D3}"),
                    new SqliteParameter("@total", (i + 1) * 50.0),
                    new SqliteParameter("@status", i < 3 ? "pending" : "shipped"),
                ]
            );
        }

        using var tx = _connection.BeginTransaction();

        // Read pending orders in transaction
        var pendingOrders = tx.Query<(string Id, string CustomerId, double Total)>(
            sql: "SELECT Id, CustomerId, Total FROM Orders WHERE Status = @status ORDER BY Total",
            parameters: [new SqliteParameter("@status", "pending")],
            mapper: r => (r.GetString(0), r.GetString(1), r.GetDouble(2))
        );
        Assert.True(
            pendingOrders
                is Result<IReadOnlyList<(string Id, string CustomerId, double Total)>, SqlError>.Ok<
                    IReadOnlyList<(string Id, string CustomerId, double Total)>,
                    SqlError
                >
        );
        if (
            pendingOrders
            is Result<IReadOnlyList<(string Id, string CustomerId, double Total)>, SqlError>.Ok<
                IReadOnlyList<(string Id, string CustomerId, double Total)>,
                SqlError
            > pendingOk
        )
        {
            var pending = pendingOk.Value;
            Assert.Equal(3, pending.Count);
            Assert.Equal(50.0, pending[0].Item3);
            Assert.Equal(100.0, pending[1].Item3);
            Assert.Equal(150.0, pending[2].Item3);
        }

        // Update all pending to confirmed
        var updateResult = tx.Execute(
            sql: "UPDATE Orders SET Status = 'confirmed' WHERE Status = @status",
            parameters: [new SqliteParameter("@status", "pending")]
        );
        Assert.True(updateResult is IntOk);
        if (updateResult is IntOk updateOk)
        {
            Assert.Equal(3, updateOk.Value);
        }

        // Verify within transaction
        AssertLongQueryValue(
            tx.Query<long>(
                sql: "SELECT COUNT(*) FROM Orders WHERE Status = 'confirmed'",
                mapper: r => r.GetInt64(0)
            ),
            expected: 3L
        );

        // Aggregate within transaction
        var totalValue = tx.Query<double>(
            sql: "SELECT SUM(Total) FROM Orders WHERE Status = 'confirmed'",
            mapper: r => r.GetDouble(0)
        );
        Assert.True(
            totalValue
                is Result<IReadOnlyList<double>, SqlError>.Ok<IReadOnlyList<double>, SqlError>
        );
        if (
            totalValue
            is Result<IReadOnlyList<double>, SqlError>.Ok<IReadOnlyList<double>, SqlError> totalOk
        )
        {
            Assert.Equal(300.0, totalOk.Value[0]);
        }

        tx.Commit();

        // Verify after commit
        AssertLongQueryValue(
            _connection.Query<long>(
                sql: "SELECT COUNT(*) FROM Orders WHERE Status = 'pending'",
                mapper: r => r.GetInt64(0)
            ),
            expected: 0L
        );
    }

    [Fact]
    public async Task DbTransactHelper_CommitAndRollbackWorkflows_WorkCorrectly()
    {
        // Use DbTransact helper for successful commit
        await _connection
            .Transact(async tx =>
            {
                if (tx is not SqliteTransaction sqliteTx || sqliteTx.Connection is not { } conn)
                    return;
                var cmd = conn.CreateCommand();
                cmd.Transaction = sqliteTx;
                cmd.CommandText =
                    "INSERT INTO Orders (Id, CustomerId, Total) VALUES (@id, @cid, @total)";
                cmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                cmd.Parameters.AddWithValue("@cid", "CUST-TX-001");
                cmd.Parameters.AddWithValue("@total", 200.0);
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            })
            .ConfigureAwait(false);

        // Verify committed
        AssertLongQueryValue(
            _connection.Query<long>(
                sql: "SELECT COUNT(*) FROM Orders WHERE CustomerId = 'CUST-TX-001'",
                mapper: r => r.GetInt64(0)
            ),
            expected: 1L
        );

        // Use DbTransact with return value
        var result = await _connection
            .Transact(async tx =>
            {
                if (tx is not SqliteTransaction sqliteTx || sqliteTx.Connection is not { } conn)
                    return 0.0;
                var cmd = conn.CreateCommand();
                cmd.Transaction = sqliteTx;
                cmd.CommandText = "SELECT Total FROM Orders WHERE CustomerId = 'CUST-TX-001'";
                var value = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
                return Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
            })
            .ConfigureAwait(false);
        Assert.Equal(200.0, result);

        // Use DbTransact with rollback on exception
        var exception = await Assert
            .ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _connection
                    .Transact(async tx =>
                    {
                        if (
                            tx is not SqliteTransaction sqliteTx
                            || sqliteTx.Connection is not { } conn
                        )
                            return;
                        var cmd = conn.CreateCommand();
                        cmd.Transaction = sqliteTx;
                        cmd.CommandText =
                            "INSERT INTO Orders (Id, CustomerId, Total) VALUES (@id, @cid, @total)";
                        cmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                        cmd.Parameters.AddWithValue("@cid", "CUST-TX-FAIL");
                        cmd.Parameters.AddWithValue("@total", 999.0);
                        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                        throw new InvalidOperationException("Simulated failure");
                    })
                    .ConfigureAwait(false);
            })
            .ConfigureAwait(false);
        Assert.Equal("Simulated failure", exception.Message);

        // Verify rolled back
        AssertLongQueryValue(
            _connection.Query<long>(
                sql: "SELECT COUNT(*) FROM Orders WHERE CustomerId = 'CUST-TX-FAIL'",
                mapper: r => r.GetInt64(0)
            ),
            expected: 0L
        );
    }

    [Fact]
    public void TransactionErrorHandling_InvalidOperationsInTransaction_ReturnsErrors()
    {
        using var tx = _connection.BeginTransaction();

        // Invalid SQL within transaction
        var badQuery = tx.Query<string>(sql: "SELECT FROM BAD SYNTAX", mapper: r => r.GetString(0));
        Assert.True(
            badQuery
                is Result<IReadOnlyList<string>, SqlError>.Error<IReadOnlyList<string>, SqlError>
        );
        if (
            badQuery
            is Result<IReadOnlyList<string>, SqlError>.Error<
                IReadOnlyList<string>,
                SqlError
            > queryError
        )
        {
            Assert.NotEmpty(queryError.Value.Message);
        }

        // Invalid execute within transaction
        var badExec = tx.Execute(sql: "INSERT INTO NONEXISTENT VALUES ('x')");
        Assert.True(badExec is IntError);

        // Invalid query within transaction (replacing Scalar)
        var badScalar = tx.Query<long>(sql: "COMPLETELY INVALID SQL", mapper: r => r.GetInt64(0));
        Assert.True(
            badScalar is Result<IReadOnlyList<long>, SqlError>.Error<IReadOnlyList<long>, SqlError>
        );

        // Null/empty SQL within transaction
        var emptyQuery = tx.Query<string>(sql: "", mapper: r => r.GetString(0));
        Assert.True(
            emptyQuery
                is Result<IReadOnlyList<string>, SqlError>.Error<IReadOnlyList<string>, SqlError>
        );

        var emptyExec = tx.Execute(sql: "  ");
        Assert.True(emptyExec is IntError);

        var emptyScalar = tx.Query<long>(sql: "", mapper: r => r.GetInt64(0));
        Assert.True(
            emptyScalar
                is Result<IReadOnlyList<long>, SqlError>.Error<IReadOnlyList<long>, SqlError>
        );
    }
}
