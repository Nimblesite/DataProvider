using Microsoft.Data.Sqlite;
using Nimblesite.Lql.SQLite;
using Nimblesite.Sql.Model;
using static global::Tests.Shared.ResultAssertions;
using static Nimblesite.DataProvider.Example.MapFunctions;

namespace Nimblesite.DataProvider.Example.Tests;

#pragma warning disable CS1591

/// <summary>
/// Tests for Program.cs demo methods and MapFunctions coverage
/// </summary>
public sealed partial class ProgramCoverageTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnection _connection;

    public ProgramCoverageTests()
    {
        (_dbPath, _connection) = global::Tests.Shared.TempSqliteDatabase.CreateClosed(
            "program_coverage_tests"
        );
    }

    [Fact]
    public async Task TestGeneratedQueriesAsync_WithValidData_Succeeds()
    {
        await SetupTestDatabase();
        await Program.TestGeneratedQueriesAsync(_connection);
    }

    [Fact]
    public async Task TestInvoiceQueryAsync_WithValidData_Succeeds()
    {
        await SetupTestDatabase();
        await Program.TestInvoiceQueryAsync(_connection);
    }

    [Fact]
    public async Task TestCustomerQueryAsync_WithValidData_Succeeds()
    {
        await SetupTestDatabase();
        await Program.TestCustomerQueryAsync(_connection);
    }

    [Fact]
    public async Task TestOrderQueryAsync_WithValidData_Succeeds()
    {
        await SetupTestDatabase();
        await Program.TestOrderQueryAsync(_connection);
    }

    [Fact]
    public async Task DemonstrateAdvancedQueryBuilding_WithValidData_Succeeds()
    {
        await SetupTestDatabase();
        Program.DemonstrateAdvancedQueryBuilding(_connection);
    }

    [Fact]
    public async Task DemoLinqQuerySyntax_WithValidData_LoadsCustomers()
    {
        await SetupTestDatabase();
        Program.DemoLinqQuerySyntax(_connection);
    }

    [Fact]
    public async Task DemoFluentQueryBuilder_WithValidData_LoadsHighValueOrders()
    {
        await SetupTestDatabase();
        Program.DemoFluentQueryBuilder(_connection);
    }

    [Fact]
    public async Task DemoLinqMethodSyntax_WithValidData_LoadsRecentOrders()
    {
        await SetupTestDatabase();
        Program.DemoLinqMethodSyntax(_connection);
    }

    [Fact]
    public void DemoComplexAggregation_Succeeds()
    {
        Program.DemoComplexAggregation();
    }

    [Fact]
    public async Task DemoComplexFiltering_WithValidData_LoadsPremiumOrders()
    {
        await SetupTestDatabase();
        Program.DemoComplexFiltering(_connection);
    }

    [Fact]
    public async Task DemonstratePredicateBuilder_WithValidData_Succeeds()
    {
        await SetupTestDatabase();
        Program.DemonstratePredicateBuilder(_connection);
    }

    [Fact]
    public void ShowcaseSummary_Succeeds()
    {
        Program.ShowcaseSummary();
    }

    [Fact]
    public async Task MapBasicOrder_WithJoinQuery_ReturnsCorrectData()
    {
        await SetupTestDatabase();

        var result = _connection.GetRecords(
            DataProviderIntegrationTests
                .BuildOrdersWithCustomer()
                .OrderBy("o.OrderNumber")
                .ToSqlStatement(),
            stmt => stmt.ToSQLite(),
            MapBasicOrder
        );

        var orders = Ok(
            actual: result,
            message: $"Expected success but got {result.GetType()}"
        ).Value;
        Assert.Equal(2, orders.Count);

        var firstOrder = orders[0];
        Assert.Equal("ORD-001", firstOrder.OrderNumber);
        Assert.Equal(500.00, firstOrder.TotalAmount);
        Assert.Equal("Completed", firstOrder.Status);
        Assert.Equal("Acme Corp", firstOrder.CustomerName);
        Assert.Equal("contact@acme.com", firstOrder.Email);

        var secondOrder = orders[1];
        Assert.Equal("ORD-002", secondOrder.OrderNumber);
        Assert.Equal(750.00, secondOrder.TotalAmount);
        Assert.Equal("Processing", secondOrder.Status);
        Assert.Equal("Tech Solutions", secondOrder.CustomerName);
        Assert.Equal("info@techsolutions.com", secondOrder.Email);
    }

    [Fact]
    public async Task MapBasicOrder_WithHighValueFilter_ReturnsFilteredResults()
    {
        await SetupTestDatabase();

        var orders = Ok(read: () =>
            _connection.GetRecords(
                DataProviderIntegrationTests
                    .BuildOrdersWithCustomer()
                    .Where("o.TotalAmount", ComparisonOperator.GreaterThan, "600.00")
                    .OrderByDescending("o.TotalAmount")
                    .Take(5)
                    .ToSqlStatement(),
                stmt => stmt.ToSQLite(),
                MapBasicOrder
            )
        ).Value;
        Assert.Single(orders);
        Assert.Equal("ORD-002", orders[0].OrderNumber);
        Assert.Equal(750.00, orders[0].TotalAmount);
    }

    [Fact]
    public async Task MapCustomer_WithNullEmail_ReturnsNullEmail()
    {
        await SetupTestDatabaseWithNullableFields();

        var result = _connection.GetRecords(
            (
                from customer in SelectStatement.From<Generated.Customer>()
                orderby customer.CustomerName
                select customer
            ).ToSqlStatement(),
            stmt => stmt.ToSQLite(),
            MapCustomer
        );

        var customers = Ok(
            actual: result,
            message: $"Expected success but got {result.GetType()}"
        ).Value;

        var nullEmailCustomer = customers.First(c => c.CustomerName == "No Email Corp");
        Assert.Null(nullEmailCustomer.Email);
        Assert.Equal("555-9999", nullEmailCustomer.Phone);
    }

    [Fact]
    public async Task MapCustomer_WithNullPhone_ReturnsNullPhone()
    {
        var nullPhoneCustomer = await VerifySharedChecks2(
            input0: _connection,
            value0: "No Phone Corp"
        );
        Assert.Equal("nophone@test.com", nullPhoneCustomer.Email);
        Assert.Null(nullPhoneCustomer.Phone);
    }

    [Fact]
    public async Task MapCustomer_WithBothNullEmailAndPhone_ReturnsBothNull()
    {
        var nullBothCustomer = await VerifySharedChecks2(
            input0: _connection,
            value0: "No Contact Corp"
        );
        Assert.Null(nullBothCustomer.Email);
        Assert.Null(nullBothCustomer.Phone);
    }

    [Fact]
    public async Task MapBasicOrder_WithLeftJoinAndComplexFilter_ReturnsCorrectResults()
    {
        await SetupTestDatabase();

        var orders = Ok(read: () =>
            _connection.GetRecords(
                "Orders"
                    .From("o")
                    .LeftJoin("Customer", "CustomerId", "Id", "o", "c")
                    .Select(
                        ("o", "OrderNumber"),
                        ("o", "TotalAmount"),
                        ("o", "Status"),
                        ("c", "CustomerName"),
                        ("c", "Email")
                    )
                    .AddWhereCondition(WhereCondition.OpenParen())
                    .Where("o.TotalAmount", ComparisonOperator.GreaterOrEq, "500.00")
                    .AddWhereCondition(WhereCondition.And())
                    .AddWhereCondition(
                        WhereCondition.Comparison(
                            ColumnInfo.Named("o.OrderDate"),
                            ComparisonOperator.GreaterOrEq,
                            "2024-01-01"
                        )
                    )
                    .AddWhereCondition(WhereCondition.CloseParen())
                    .Or("o.Status", "VIP")
                    .OrderBy("o.OrderDate")
                    .Take(3)
                    .ToSqlStatement(),
                stmt => stmt.ToSQLite(),
                MapBasicOrder
            )
        ).Value;
        Assert.NotEmpty(orders);

        foreach (var order in orders)
        {
            Assert.False(string.IsNullOrEmpty(order.OrderNumber));
            Assert.False(string.IsNullOrEmpty(order.CustomerName));
        }
    }

    [Fact]
    public async Task ProgramMain_RunsSuccessfully()
    {
        var originalDir = Environment.CurrentDirectory;
        var tempDir = Path.Combine(Path.GetTempPath(), $"program_main_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        try
        {
            Environment.CurrentDirectory = tempDir;
            await Program.Main([]).ConfigureAwait(false);
        }
        finally
        {
            Environment.CurrentDirectory = originalDir;
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
#pragma warning disable CA1031
            catch (IOException)
            { /* best effort */
            }
#pragma warning restore CA1031
        }
    }

    [Fact]
    public void ModelTypes_CanBeConstructed()
    {
        // Cover Example.Model record constructors
        var order = Dataset_ModelTypes_CanBeConstructed_1();

        Assert.Equal("ORD-001", order.OrderNumber);
        Assert.Single(order.Items);
        Assert.Equal("Widget", order.Items[0].ProductName);

        var customer = Dataset_ModelTypes_CanBeConstructed_2();

        Assert.Equal("Test Corp", customer.CustomerName);
        Assert.Single(customer.Addresses);
        Assert.Equal("TestCity", customer.Addresses[0].City);
    }

    [Fact]
    public void ModelRecords_EqualityWorks()
    {
        var order1 = Dataset_ModelRecords_EqualityWorks_1();
        var order2 = Dataset_ModelRecords_EqualityWorks_2();
        AssertRecordEquality(order1, order2);
        Assert.Equal(order1.ToString(), order2.ToString());

        var item1 = Dataset_ModelRecords_EqualityWorks_3();
        var item2 = Dataset_ModelRecords_EqualityWorks_4();
        AssertRecordEquality(item1, item2);

        var addr1 = Dataset_ModelRecords_EqualityWorks_5();
        var addr2 = Dataset_ModelRecords_EqualityWorks_6();
        AssertRecordEquality(addr1, addr2);

        var cust1 = Dataset_ModelRecords_EqualityWorks_7();
        var cust2 = Dataset_ModelRecords_EqualityWorks_8();
        AssertRecordEquality(cust1, cust2);

        var basic1 = Dataset_ModelRecords_EqualityWorks_9();
        var basic2 = Dataset_ModelRecords_EqualityWorks_10();
        AssertRecordEquality(basic1, basic2);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void AssertRecordEquality<T>(T first, T second)
        where T : notnull
    {
        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    private async Task SetupTestDatabase()
    {
        await global::Nimblesite
            .DataProvider.Example.Tests.DataProviderIntegrationTests.VerifySharedPhase2(
                input0: _connection
            )
            .ConfigureAwait(false);

        await global::Nimblesite
            .DataProvider.Example.Tests.GeneratedOperationsCoverageTests.VerifyUnifiedWorkflow101(
                configure: command => command.CommandText = Data_SetupTestDatabase_1,
                input1: _connection
            )
            .ConfigureAwait(false);
    }

    private async Task SetupTestDatabaseWithNullableFields()
    {
        await VerifyReusableWorkflow3(
                configure0: command =>
                    command.CommandText = Data_SetupTestDatabaseWithNullableFields_1,
                configure1: command =>
                    command.CommandText = Data_SetupTestDatabaseWithNullableFields_2,
                input0: _connection
            )
            .ConfigureAwait(false);
    }

    public void Dispose() =>
        CoreCoverageTests.DisposeSqliteFixture(connection: _connection, dbPath: _dbPath);

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private async global::System.Threading.Tasks.Task<Customer> VerifySharedChecks2(
        SqliteConnection input0,
        string value0
    )
    {
        await SetupTestDatabaseWithNullableFields().ConfigureAwait(false);

        var customers = Ok(read: () =>
            input0.GetRecords(
                (
                    from customer in SelectStatement.From<Generated.Customer>()
                    orderby customer.CustomerName
                    select customer
                ).ToSqlStatement(),
                stmt => stmt.ToSQLite(),
                MapCustomer
            )
        ).Value;

        var nullPhoneCustomer = customers.First(c => c.CustomerName == value0);

        return nullPhoneCustomer;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static async global::System.Threading.Tasks.Task VerifyReusableWorkflow3(
        SqliteConnection input0,
        global::System.Action<global::Microsoft.Data.Sqlite.SqliteCommand> configure0,
        global::System.Action<global::Microsoft.Data.Sqlite.SqliteCommand> configure1
    )
    {
        await global::Nimblesite
            .DataProvider.Example.Tests.GeneratedOperationsCoverageTests.VerifyUnifiedWorkflow102(
                input0: input0
            )
            .ConfigureAwait(false);

        await global::Nimblesite
            .DataProvider.Example.Tests.CoreCoverageTests.VerifySharedPhase1(
                configure0: configure0,
                configure1: configure1,
                input0: input0
            )
            .ConfigureAwait(false);
    }
}
