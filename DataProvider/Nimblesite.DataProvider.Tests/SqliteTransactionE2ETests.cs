// Implements [CI-TEST-SHARED-COMMAND].

namespace Nimblesite.DataProvider.Tests;

/// <summary>
/// E2E tests: transaction workflows with real SQLite databases.
/// Tests commit, rollback, multi-table transactions, and DbTransact helpers.
/// </summary>
public sealed partial class SqliteTransactionE2ETests : IDisposable
{
    private readonly string _dbPath = global::Tests.Shared.TempSqliteDatabase.PathFor("tx_e2e");

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
        VerifyReusableWorkflow19(
            configure0: command => command.CommandText = Data_CreateSchema_1,
            configure1: command => command.CommandText = Data_CreateSchema_2,
            input0: _connection
        );
    }

    private static void AssertLongQueryValue(
        Result<IReadOnlyList<long>, SqlError> result,
        long expected
    )
    {
        global::Tests.Shared.ResultAssertions.VerifyOk(
            actual: result,
            verify: ok =>
            {
                Assert.Equal(expected, ok.Value[0]);
            }
        );
    }

    private static void AssertLongSql(
        System.Data.IDbConnection connection,
        string sql,
        long expected
    ) =>
        AssertLongQueryValue(
            connection.Query<long>(sql: sql, mapper: r => r.GetInt64(0)),
            expected: expected
        );

    private static void AssertLongSql(
        System.Data.IDbTransaction connection,
        string sql,
        long expected
    ) =>
        AssertLongQueryValue(
            connection.Query<long>(sql: sql, mapper: r => r.GetInt64(0)),
            expected: expected
        );

    // Implements [CI-TEST-SHARED-SCENARIOS]: runs a sequence of scalar long checks
    // in order against the same connection.
    private static void AssertLongSqls(
        System.Data.IDbConnection connection,
        params (string Sql, long Expected)[] checks
    )
    {
        foreach (var (sql, expected) in checks)
            AssertLongSql(connection: connection, sql: sql, expected: expected);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS]: inserts one order through the
    // DbTransact helper when the receiver is a concrete SQLite transaction.
    private static async Task InsertOrderIfSqliteAsync(
        System.Data.IDbTransaction tx,
        string customerId,
        double total
    )
    {
        if (tx is not SqliteTransaction sqliteTx || sqliteTx.Connection is not { } conn)
            return;
        await VerifyReusableWorkflow21(
                input0: conn,
                input1: sqliteTx,
                value3: customerId,
                value5: total
            )
            .ConfigureAwait(false);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void InsertOrderItem(
        SqliteTransaction tx,
        string id,
        string orderId,
        string name,
        int qty,
        double price
    ) =>
        tx.Execute(
            sql: "INSERT INTO OrderItems (Id, OrderId, ProductName, Quantity, UnitPrice) VALUES (@id, @oid, @name, @qty, @price)",
            parameters:
            [
                new SqliteParameter("@id", id),
                new SqliteParameter("@oid", orderId),
                new SqliteParameter("@name", name),
                new SqliteParameter("@qty", qty),
                new SqliteParameter("@price", price),
            ]
        );

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void DecrementInventory(SqliteTransaction tx, int qty, string name) =>
        tx.Execute(
            sql: "UPDATE Inventory SET StockCount = StockCount - @qty WHERE ProductName = @name",
            parameters: [new SqliteParameter("@qty", qty), new SqliteParameter("@name", name)]
        );

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
        global::Nimblesite.DataProvider.Tests.DbTransactionExtensionsTests.VerifyReusableWorkflow16(
            input0: orderInsert,
            value0: 1
        );

        // Insert order items within same transaction
        var item1Id = Guid.NewGuid().ToString();
        var item2Id = Guid.NewGuid().ToString();
        InsertOrderItem(
            tx: tx,
            id: item1Id,
            orderId: orderId,
            name: "Widget",
            qty: 3,
            price: 25.25
        );
        InsertOrderItem(
            tx: tx,
            id: item2Id,
            orderId: orderId,
            name: "Gadget",
            qty: 2,
            price: 37.50
        );

        // Update inventory within transaction
        DecrementInventory(tx: tx, qty: 3, name: "Widget");
        DecrementInventory(tx: tx, qty: 2, name: "Gadget");

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
        AssertLongSqls(
            connection: _connection,
            checks:
            [
                ("SELECT COUNT(*) FROM Orders", 1L),
                ("SELECT COUNT(*) FROM OrderItems", 2L),
                ("SELECT StockCount FROM Inventory WHERE ProductName = 'Widget'", 97L),
                ("SELECT StockCount FROM Inventory WHERE ProductName = 'Gadget'", 48L),
            ]
        );
    }

    [Fact]
    public void TransactionRollback_FailedOperation_NoDataPersisted()
    {
        // Verify initial state
        AssertLongSql(connection: _connection, sql: "SELECT COUNT(*) FROM Orders", expected: 0L);

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
        InsertOrderItem(
            tx: tx,
            id: Guid.NewGuid().ToString(),
            orderId: orderId,
            name: "Widget",
            qty: 5,
            price: 19.99
        );

        // Verify data exists within transaction
        AssertLongSql(connection: tx, sql: "SELECT COUNT(*) FROM Orders", expected: 1L);

        // Rollback
        tx.Rollback();

        // Verify NO data persisted
        AssertLongSqls(
            connection: _connection,
            checks:
            [
                ("SELECT COUNT(*) FROM Orders", 0L),
                ("SELECT COUNT(*) FROM OrderItems", 0L),
                ("SELECT StockCount FROM Inventory WHERE ProductName = 'Widget'", 100L),
            ]
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
        global::Tests.Shared.ResultAssertions.VerifyOk(
            actual: pendingOrders,
            verify: pendingOk =>
            {
                var pending = pendingOk.Value;
                Assert.Equal(3, pending.Count);
                Assert.Equal(50.0, pending[0].Item3);
                Assert.Equal(100.0, pending[1].Item3);
                Assert.Equal(150.0, pending[2].Item3);
            }
        );

        // Update all pending to confirmed
        var updateResult = tx.Execute(
            sql: "UPDATE Orders SET Status = 'confirmed' WHERE Status = @status",
            parameters: [new SqliteParameter("@status", "pending")]
        );
        global::Nimblesite.DataProvider.Tests.DbTransactionExtensionsTests.VerifyReusableWorkflow16(
            input0: updateResult,
            value0: 3
        );

        // Verify within transaction
        AssertLongSql(
            connection: tx,
            sql: "SELECT COUNT(*) FROM Orders WHERE Status = 'confirmed'",
            expected: 3L
        );

        // Aggregate within transaction
        var totalValue = tx.Query<double>(
            sql: "SELECT SUM(Total) FROM Orders WHERE Status = 'confirmed'",
            mapper: r => r.GetDouble(0)
        );
        global::Tests.Shared.ResultAssertions.VerifyOk(
            actual: totalValue,
            verify: totalOk =>
            {
                Assert.Equal(300.0, totalOk.Value[0]);
            }
        );

        tx.Commit();

        // Verify after commit
        AssertLongSql(
            connection: _connection,
            sql: "SELECT COUNT(*) FROM Orders WHERE Status = 'pending'",
            expected: 0L
        );
    }

    [Fact]
    public async Task DbTransactHelper_CommitAndRollbackWorkflows_WorkCorrectly()
    {
        // Use DbTransact helper for successful commit
        await _connection
            .Transact(async tx =>
                await InsertOrderIfSqliteAsync(tx: tx, customerId: "CUST-TX-001", total: 200.0)
                    .ConfigureAwait(false)
            )
            .ConfigureAwait(false);

        // Verify committed
        AssertLongSql(
            connection: _connection,
            sql: "SELECT COUNT(*) FROM Orders WHERE CustomerId = 'CUST-TX-001'",
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
                        await InsertOrderIfSqliteAsync(
                                tx: tx,
                                customerId: "CUST-TX-FAIL",
                                total: 999.0
                            )
                            .ConfigureAwait(false);
                        throw new InvalidOperationException("Simulated failure");
                    })
                    .ConfigureAwait(false);
            })
            .ConfigureAwait(false);
        Assert.Equal("Simulated failure", exception.Message);

        // Verify rolled back
        AssertLongSql(
            connection: _connection,
            sql: "SELECT COUNT(*) FROM Orders WHERE CustomerId = 'CUST-TX-FAIL'",
            expected: 0L
        );
    }

    [Fact]
    public void TransactionErrorHandling_InvalidOperationsInTransaction_ReturnsErrors()
    {
        using var tx = _connection.BeginTransaction();

        // Invalid SQL within transaction
        var badQuery = tx.Query<string>(sql: "SELECT FROM BAD SYNTAX", mapper: r => r.GetString(0));
        var queryError = global::Tests.Shared.ResultAssertions.Error(actual: badQuery).Value;
        Assert.NotEmpty(queryError.Message);

        // Invalid execute within transaction
        VerifyReusableWorkflow22(
            input0: tx,
            value0: "INSERT INTO NONEXISTENT VALUES ('x')",
            value1: "COMPLETELY INVALID SQL"
        );

        // Null/empty SQL within transaction
        var emptyQuery = tx.Query<string>(sql: "", mapper: r => r.GetString(0));
        _ = global::Tests.Shared.ResultAssertions.Error(actual: emptyQuery);

        VerifyReusableWorkflow22(input0: tx, value0: "  ", value1: "");
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyReusableWorkflow19(
        SqliteConnection input0,
        global::System.Action<global::System.Data.Common.DbCommand> configure0,
        global::System.Action<global::System.Data.Common.DbCommand> configure1
    )
    {
        DatabaseCommands.Execute(connection: input0, configure: configure0);

        // Seed inventory
        DatabaseCommands.Execute(connection: input0, configure: configure1);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static async global::System.Threading.Tasks.Task VerifyReusableWorkflow21(
        SqliteConnection input0,
        SqliteTransaction input1,
        string value3,
        double value5
    )
    {
        var cmd = input0.CreateCommand();
        cmd.Transaction = input1;
        cmd.CommandText = "INSERT INTO Orders (Id, CustomerId, Total) VALUES (@id, @cid, @total)";
        cmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
        cmd.Parameters.AddWithValue("@cid", value3);
        cmd.Parameters.AddWithValue("@total", value5);
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyReusableWorkflow22(
        SqliteTransaction input0,
        string value0,
        string value1
    )
    {
        // Invalid execute within transaction
        global::Tests.Shared.ResultAssertions.Error(actual: input0.Execute(sql: value0));

        // Invalid query within transaction (replacing Scalar)
        var badScalar = input0.Query<long>(sql: value1, mapper: r => r.GetInt64(0));
        _ = global::Tests.Shared.ResultAssertions.Error(actual: badScalar);
    }
}
