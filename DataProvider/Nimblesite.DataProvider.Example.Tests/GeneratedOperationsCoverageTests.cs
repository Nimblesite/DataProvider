using System.Data;
using Microsoft.Data.Sqlite;
using AffectedRowsResult = Outcome.Result<int, Nimblesite.Sql.Model.SqlError>;

namespace Nimblesite.DataProvider.Example.Tests;

#pragma warning disable CS1591

/// <summary>
/// Tests for generated Insert/Update operations and SampleDataSeeder
/// </summary>
public sealed partial class GeneratedOperationsCoverageTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnection _connection;

    public GeneratedOperationsCoverageTests()
    {
        (_dbPath, _connection) = global::Tests.Shared.TempSqliteDatabase.CreateClosed(
            "generated_ops_tests"
        );
    }

    [Fact]
    public async Task SampleDataSeeder_SeedDataAsync_InsertsAllEntities()
    {
        await SetupSchema().ConfigureAwait(false);

        var (flowControl, result) = await _connection
            .Transact(async tx => await SampleDataSeeder.SeedDataAsync(tx).ConfigureAwait(false))
            .ConfigureAwait(false);

        Assert.True(flowControl);
        Assert.True(result is StringSqlOk);
    }

    [Fact]
    public async Task InsertCustomerAsync_WithValidData_ReturnsOk()
    {
        await AssertAffectedRows(
                execute: tx =>
                    tx.InsertCustomerAsync(
                        Guid.NewGuid().ToString(),
                        "Test Customer",
                        "test@test.com",
                        "555-1234",
                        "2024-01-01"
                    ),
                expectedRows: 1
            )
            .ConfigureAwait(false);
    }

    [Fact]
    public async Task InsertInvoiceAsync_WithValidData_ReturnsOk()
    {
        await AssertAffectedRows(
                execute: tx =>
                    tx.InsertInvoiceAsync(
                        Guid.NewGuid().ToString(),
                        "INV-TEST-001",
                        "2024-06-01",
                        "Test Corp",
                        "billing@test.com",
                        1500.00,
                        null,
                        "Test invoice"
                    ),
                expectedRows: 1
            )
            .ConfigureAwait(false);
    }

    [Fact]
    public async Task InsertInvoiceLineAsync_WithValidData_ReturnsOk()
    {
        await SetupSchemaAndSeed().ConfigureAwait(false);

        await _connection
            .Transact(async tx =>
            {
                var invoiceId = VerifySharedContinuation2(
                    input0: tx,
                    value0: "SELECT Id FROM Invoice LIMIT 1"
                );

                var result = await tx.InsertInvoiceLineAsync(
                        Guid.NewGuid().ToString(),
                        invoiceId,
                        "Test Line Item",
                        2.0,
                        75.00,
                        150.00,
                        5.0,
                        "Test notes"
                    )
                    .ConfigureAwait(false);
                Assert.Equal(
                    1,
                    global::Tests.Shared.ResultAssertions.ExactOk(actual: result).Value
                );
            })
            .ConfigureAwait(false);
    }

    [Fact]
    public async Task InsertAddressAsync_WithValidData_ReturnsOk()
    {
        await SetupSchemaAndSeed().ConfigureAwait(false);

        await _connection
            .Transact(async tx =>
            {
                var customers = CoreCoverageTests.QueryStringList(
                    transaction: tx,
                    sql: "SELECT Id FROM Customer LIMIT 1"
                );

                var result = await tx.InsertAddressAsync(
                        Guid.NewGuid().ToString(),
                        customers[0],
                        "100 Test St",
                        "TestCity",
                        "TS",
                        "12345",
                        "USA"
                    )
                    .ConfigureAwait(false);
                Assert.Equal(
                    1,
                    global::Tests.Shared.ResultAssertions.ExactOk(actual: result).Value
                );
            })
            .ConfigureAwait(false);
    }

    [Fact]
    public async Task InsertOrdersAsync_WithValidData_ReturnsOk()
    {
        await SetupSchemaAndSeed().ConfigureAwait(false);

        await _connection
            .Transact(async tx =>
            {
                var customers = CoreCoverageTests.QueryStringList(
                    transaction: tx,
                    sql: "SELECT Id FROM Customer LIMIT 1"
                );

                var result = await tx.InsertOrdersAsync(
                        Guid.NewGuid().ToString(),
                        "ORD-TEST-001",
                        "2024-06-01",
                        customers[0],
                        999.99,
                        "Pending"
                    )
                    .ConfigureAwait(false);
                Assert.Equal(
                    1,
                    global::Tests.Shared.ResultAssertions.ExactOk(actual: result).Value
                );
            })
            .ConfigureAwait(false);
    }

    [Fact]
    public async Task InsertOrderItemAsync_WithValidData_ReturnsOk()
    {
        await SetupSchemaAndSeed().ConfigureAwait(false);

        await _connection
            .Transact(async tx =>
            {
                var orders = CoreCoverageTests.QueryStringList(
                    transaction: tx,
                    sql: "SELECT Id FROM Orders LIMIT 1"
                );

                var result = await tx.InsertOrderItemAsync(
                        Guid.NewGuid().ToString(),
                        orders[0],
                        "Test Widget",
                        5.0,
                        25.00,
                        125.00
                    )
                    .ConfigureAwait(false);
                Assert.Equal(
                    1,
                    global::Tests.Shared.ResultAssertions.ExactOk(actual: result).Value
                );
            })
            .ConfigureAwait(false);
    }

    private async Task SetupSchema()
    {
        await VerifyUnifiedWorkflow102(input0: _connection).ConfigureAwait(false);

        await VerifyUnifiedWorkflow101(
                configure: command => command.CommandText = Data_SetupSchema_1,
                input1: _connection
            )
            .ConfigureAwait(false);
    }

    // Implements [CI-DESLOP]: retain the result type and row-count assertions
    // for each generated operation while sharing schema and transaction setup.
    private async Task AssertAffectedRows(
        Func<IDbTransaction, Task<AffectedRowsResult>> execute,
        int expectedRows,
        bool seed = false
    )
    {
        await (seed ? SetupSchemaAndSeed() : SetupSchema()).ConfigureAwait(false);
        await _connection
            .Transact(async transaction =>
            {
                var result = await execute(transaction).ConfigureAwait(false);
                Assert.Equal(
                    expectedRows,
                    global::Tests.Shared.ResultAssertions.ExactOk(actual: result).Value
                );
            })
            .ConfigureAwait(false);
    }

    private async Task SetupSchemaAndSeed()
    {
        await SetupSchema().ConfigureAwait(false);

        await VerifyUnifiedWorkflow101(
                configure: command => command.CommandText = Data_SetupSchemaAndSeed_1,
                input1: _connection
            )
            .ConfigureAwait(false);
    }

    public void Dispose() =>
        CoreCoverageTests.DisposeSqliteFixture(connection: _connection, dbPath: _dbPath);

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static async global::System.Threading.Tasks.Task VerifyUnifiedWorkflow101(
        Action<SqliteCommand> configure,
        SqliteConnection input1
    )
    {
        using var command = new SqliteCommand { Connection = input1 };
        configure(command);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static async global::System.Threading.Tasks.Task VerifyUnifiedWorkflow102(
        SqliteConnection input0
    )
    {
        await input0.OpenAsync().ConfigureAwait(false);
        using (var pragmaCommand = new SqliteCommand("PRAGMA foreign_keys = OFF", input0))
        {
            await pragmaCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static string VerifySharedContinuation2(IDbTransaction input0, string value0) =>
        CoreCoverageTests.QueryStringList(transaction: input0, sql: value0)[0];
}
