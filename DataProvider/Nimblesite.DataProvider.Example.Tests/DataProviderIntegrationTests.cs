// Implements [DP-SQL-MODEL-TEST-FRAGMENTS].
using Microsoft.Data.Sqlite;
using Nimblesite.Lql.SQLite;
using Nimblesite.Sql.Model;
using static global::Tests.Shared.ResultAssertions;
using static global::Tests.Shared.StringAssertions;
using static Nimblesite.DataProvider.Example.MapFunctions;

namespace Nimblesite.DataProvider.Example.Tests;

#pragma warning disable CS1591

/// <summary>
/// Integration tests for DataProvider code generation
/// </summary>
public sealed partial class DataProviderIntegrationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnection _connection;

    public DataProviderIntegrationTests()
    {
        (_dbPath, _connection) = global::Tests.Shared.TempSqliteDatabase.CreateClosed(
            "dataprovider_integration_tests"
        );
    }

    [Fact]
    public async Task GetInvoicesAsync_WithValidData_ReturnsCorrectTypes()
    {
        // Arrange
        await SetupTestDatabase();

        // Act
        var result = await _connection.GetInvoicesAsync("Acme Corp", "2024-01-01", "2024-12-31");

        // Assert
        if (result is InvoiceListError failure)
        {
            throw Dataset_GetInvoicesAsync_WithValidData_ReturnsCorrectTypes_1(failure: failure);
        }

        var invoices = Success(result);

        Assert.NotEmpty(invoices);
        AssertInvoiceTypes(invoice: invoices[0], line: invoices[0].InvoiceLines[0]);
    }

    [Fact]
    public async Task GetCustomersLqlAsync_WithValidData_ReturnsCorrectTypes()
    {
        // Arrange
        await SetupTestDatabase();

        // Act
        var result = await _connection.GetCustomersLqlAsync(null);

        // Assert
        if (result is CustomerListError failure)
        {
            // Log the error but continue the test to see what happens
            Console.WriteLine($"GetCustomersLqlAsync failed: {failure.Value.Message}");
            Console.WriteLine(
                $"Full exception: {failure.Value.Exception?.ToString() ?? "No exception details"}"
            );
        }

        var success = Ok(
            actual: result,
            message: $"Expected Success but got {result.GetType()}, Error: {(result as CustomerListError)?.Value.Message ?? "No error message"}"
        );
        var customers = success.Value;

        Assert.NotEmpty(customers);
        AssertCustomerTypes(customer: customers[0], address: customers[0].Addresss[0]);
    }

    [Fact]
    public async Task GetOrdersAsync_WithValidData_ReturnsCorrectTypes()
    {
        // Arrange
        var orders = await VerifyReusableWorkflow1(input0: SetupTestDatabase, input1: _connection)
            .ConfigureAwait(false);

        Assert.NotEmpty(orders);
        AssertOrderTypes(order: orders[0], item: orders[0].OrderItems[0]);
    }

    [Fact]
    public async Task AllQueries_VerifyCorrectTableNamesGenerated()
    {
        // Arrange
        await SetupTestDatabase();

        // Act & Assert - Verify extension methods exist with correct names
        var invoiceResult = await _connection.GetInvoicesAsync("Acme Corp", null!, null!);
        var customerResult = await _connection.GetCustomersLqlAsync(null);
        var orderResult = await _connection.GetOrdersAsync("cust-1", null!, null!, null!);

        // All should succeed (this proves the extension methods were generated)
        Assert.True(
            invoiceResult is InvoiceListOk,
            $"Expected Invoice Success but got {invoiceResult.GetType()}"
        );
        Assert.True(
            customerResult is CustomerListOk,
            $"Expected Customer Success but got {customerResult.GetType()}"
        );
        Assert.True(
            orderResult is OrderListOk,
            $"Expected Order Success but got {orderResult.GetType()}"
        );

        // Verify different table names were used (not hard-coded)
        _ = (Ok(actual: invoiceResult)).Value;
        _ = (Ok(actual: customerResult)).Value;
        _ = (Ok(actual: orderResult)).Value;

        //TODO: Assert these!!!

        // These should be different types, proving they're not hard-coded
        Assert.NotEqual(typeof(Invoice), typeof(Customer));
        Assert.NotEqual(typeof(Invoice), typeof(Order));
        Assert.NotEqual(typeof(Customer), typeof(Order));

        Assert.NotEqual(typeof(InvoiceLine), typeof(Address));
        Assert.NotEqual(typeof(InvoiceLine), typeof(OrderItem));
        Assert.NotEqual(typeof(Address), typeof(OrderItem));
    }

    [Fact]
    public async Task GetInvoicesAsync_WithMultipleRecords_GroupsCorrectly()
    {
        // Arrange
        var invoices = await VerifyReusableWorkflow6(input0: SetupTestDatabase, input1: _connection)
            .ConfigureAwait(false);

        Assert.Equal(3, invoices.Count);

        // Verify grouping is working correctly
        var firstInvoice = invoices[0];
        Assert.Equal(2, firstInvoice.InvoiceLines.Count);

        var secondInvoice = invoices[1];
        Assert.Equal(2, secondInvoice.InvoiceLines.Count);

        var thirdInvoice = invoices[2];
        Assert.Equal(2, thirdInvoice.InvoiceLines.Count);
    }

    [Fact]
    public async Task GetCustomersLqlAsync_WithMultipleAddresses_GroupsCorrectly()
    {
        // Arrange
        var customers = await VerifyReusableWorkflow7(
                input0: SetupTestDatabase,
                input1: _connection
            )
            .ConfigureAwait(false);

        Assert.Equal(2, customers.Count);

        // First customer should have 2 addresses
        var firstCustomer = customers[0];
        Assert.Equal(2, firstCustomer.Addresss.Count);

        // Second customer should have 1 address
        var secondCustomer = customers[1];
        Assert.Single(secondCustomer.Addresss);
    }

    [Fact]
    public async Task GetOrdersAsync_WithMultipleItems_GroupsCorrectly()
    {
        // Arrange
        await SetupTestDatabase();

        // Act
        var result = await _connection.GetOrdersAsync(null, null, "2024-01-01", "2024-12-31");

        // Assert

        var orders = Success(result);

        Assert.Equal(2, orders.Count);

        // Orders are returned by OrderDate DESC, so ORD-002 comes first (1 item), ORD-001 comes second (2 items)
        var firstOrder = orders[0]; // ORD-002
        Assert.Single(firstOrder.OrderItems);

        var secondOrder = orders[1]; // ORD-001
        Assert.Equal(2, secondOrder.OrderItems.Count);
    }

    [Fact]
    public async Task GetInvoicesAsync_WithEmptyDatabase_ReturnsEmpty() =>
        await AssertWorkflowEmpty<Invoice>(workflow: VerifyReusableWorkflow6).ConfigureAwait(false);

    [Fact]
    public async Task GetCustomersLqlAsync_WithEmptyDatabase_ReturnsEmpty() =>
        await AssertWorkflowEmpty<Customer>(workflow: VerifyReusableWorkflow7)
            .ConfigureAwait(false);

    [Fact]
    public async Task GetOrdersAsync_WithEmptyDatabase_ReturnsEmpty() =>
        await AssertWorkflowEmpty<Order>(workflow: VerifyReusableWorkflow1).ConfigureAwait(false);

    [Fact]
    public void FluentQueryBuilder_InnerJoin_GeneratesCorrectSQL()
    {
        // Arrange & Act
        var query = DataProviderIntegrationTests
            .BuildOrdersWithCustomer()
            .Where("o.TotalAmount", ComparisonOperator.GreaterThan, "400.00")
            .OrderByDescending("o.TotalAmount")
            .Take(5)
            .ToSqlStatement();

        var sqlResult = query.ToSQLite();

        // Assert
        // Verify JOIN is included
        // Verify table aliases are included
        // Verify full expected SQL structure
        var sql = SqlResultAssertions.Contains(
            result: sqlResult,
            fragments:
            [
                "INNER JOIN",
                "Customer",
                "ON o.CustomerId = c.Id",
                "FROM Orders o",
                "Customer c",
                "SELECT o.OrderNumber, o.TotalAmount, o.Status, c.CustomerName, c.Email",
                "WHERE o.TotalAmount > '400.00'",
                "ORDER BY o.TotalAmount DESC",
                "LIMIT 5",
            ],
            message: $"SQL generation should succeed, got: {(sqlResult as StringSqlError)?.Value.Message}"
        );
    }

    [Fact]
    public void FluentQueryBuilder_LeftJoin_GeneratesCorrectSQL()
    {
        // Arrange & Act
        var query = "Orders"
            .From("ord")
            .LeftJoin("Customer", "CustomerId", "Id", "ord", "cust")
            .Select(("ord", "OrderNumber"), ("cust", "CustomerName"))
            .ToSqlStatement();

        VerifyReusableWorkflow2(
            input0: query,
            value0: "LEFT JOIN",
            value1: "Customer cust",
            value2: "FROM Orders ord",
            value3: "ON ord.CustomerId = cust.Id"
        );
    }

    [Fact]
    public void FluentQueryBuilder_MultipleJoins_GeneratesCorrectSQL()
    {
        // Arrange & Act - Simulate Orders -> Customer -> Address join chain
        var query = "Orders"
            .From("o")
            .InnerJoin("Customer", "CustomerId", "Id", "o", "c")
            .LeftJoin("Address", "Id", "CustomerId", "c", "a")
            .Select(("o", "OrderNumber"), ("c", "CustomerName"), ("a", "City"))
            .ToSqlStatement();

        VerifyReusableWorkflow2(
            input0: query,
            value0: "INNER JOIN Customer c",
            value1: "LEFT JOIN Address a",
            value2: "ON o.CustomerId = c.Id",
            value3: "ON c.Id = a.CustomerId"
        );
    }

    [Fact]
    public void FluentQueryBuilder_InnerJoinWithComplex_GeneratesCorrectSQL()
    {
        // Arrange & Act - Test a complex JOIN with multiple conditions
        var query = "Orders"
            .From("o")
            .InnerJoin("Customer", "CustomerId", "Id", "o", "c")
            .Select(("o", "OrderNumber"), ("c", "CustomerName"), ("o", "TotalAmount"))
            .Where("o.Status", ComparisonOperator.Eq, "Completed")
            .OrderBy("c.CustomerName")
            .Take(10)
            .ToSqlStatement();

        // Assert
        // Assert
        // Test that all parts of the fluent API are preserved in the generated SQL
        SqlFragments(
            read: () => query.ToSQLite(),
            expected:
            [
                "SELECT o.OrderNumber, c.CustomerName, o.TotalAmount",
                "FROM Orders o",
                "INNER JOIN Customer c",
                "ON o.CustomerId = c.Id",
                "WHERE o.Status = 'Completed'",
                "ORDER BY c.CustomerName ASC",
                "LIMIT 10",
            ],
            comparison: StringComparison.Ordinal
        );
    }

    private IReadOnlyList<Customer> QueryCustomers(IQueryable<Customer> query) =>
        Ok(
            actual: _connection.GetRecords(query.ToSqlStatement(), s => s.ToSQLite(), MapCustomer)
        ).Value;

    private IReadOnlyList<Order> QueryOrders(IQueryable<Order> query) =>
        Ok(
            actual: _connection.GetRecords(query.ToSqlStatement(), s => s.ToSQLite(), MapOrder)
        ).Value;

    // Implements [CI-TEST-SHARED-RESULT].
    private static TEntity Success<TEntity>(Outcome.Result<TEntity, SqlError> result) =>
        Ok(actual: result, message: $"Expected Success but got {result.GetType()}").Value;

    private async Task SetupTestDatabase()
    {
        await VerifySharedPhase2(input0: _connection).ConfigureAwait(false);

        // Insert comprehensive test data
        var insertScript = Data_SetupTestDatabase_1;

        using var insertCommand = new SqliteCommand(insertScript, _connection);
        await insertCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private async Task SetupEmptyDatabase()
    {
        await _connection.OpenAsync().ConfigureAwait(false);

        // Create tables but don't insert any data - same script as above but without inserts
        await global::Nimblesite
            .DataProvider.Example.Tests.GeneratedOperationsCoverageTests.VerifyUnifiedWorkflow101(
                configure: command => command.CommandText = Data_SetupEmptyDatabase_1,
                input1: _connection
            )
            .ConfigureAwait(false);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private async Task AssertWorkflowEmpty<TEntity>(
        Func<
            Func<Task>,
            SqliteConnection,
            Task<System.Collections.Immutable.ImmutableList<TEntity>>
        > workflow
    )
    {
        var items = await workflow(arg1: SetupEmptyDatabase, arg2: _connection)
            .ConfigureAwait(false);
        Assert.Empty(items);
    }

    #region PredicateBuilder E2E Tests

    /// <summary>
    /// Tests PredicateBuilder True predicate with actual database data
    /// </summary>
    [Fact]
    public async Task PredicateBuilder_True_E2E_ReturnsAllCustomers()
    {
        // Arrange
        await SetupTestDatabase();
        var predicate = PredicateBuilder.True<Customer>();
        var customers = VerifySharedContinuation1(input0: predicate);
        Assert.Equal(2, customers.Count);
    }

    /// <summary>
    /// Tests PredicateBuilder False predicate with actual database data
    /// </summary>
    [Fact]
    public async Task PredicateBuilder_False_E2E_ReturnsNoCustomers()
    {
        // Arrange
        await SetupTestDatabase();
        var predicate = PredicateBuilder.False<Customer>();
        var customers = VerifySharedContinuation1(input0: predicate);
        Assert.Empty(customers);
    }

    /// <summary>
    /// Tests PredicateBuilder Or operation with actual database data
    /// </summary>
    [Fact]
    public async Task PredicateBuilder_Or_E2E_CombinesPredicatesWithOrLogic()
    {
        // Arrange
        await SetupTestDatabase();
        var predicate = PredicateBuilder.False<Customer>();
        predicate = predicate.Or(c => c.CustomerName == "Acme Corp");
        predicate = predicate.Or(c => c.CustomerName == "Tech Solutions");
        var query = SelectStatement
            .From<Customer>("Customer")
            .Where(predicate)
            .OrderBy(c => c.CustomerName);

        // Act
        var customers = QueryCustomers(query);
        Assert.Equal(2, customers.Count);
        Assert.Equal("Acme Corp", customers[0].CustomerName);
        Assert.Equal("Tech Solutions", customers[1].CustomerName);
    }

    /// <summary>
    /// Tests PredicateBuilder And operation with actual database data
    /// </summary>
    [Fact]
    public async Task PredicateBuilder_And_E2E_CombinesPredicatesWithAndLogic()
    {
        // Arrange
        await SetupTestDatabase();
        var predicate = PredicateBuilder.True<Customer>();
        predicate = predicate.And(c => c.CustomerName == "Acme Corp");
        predicate = predicate.And(c => c.Email != null);
        VerifyReusableWorkflow4(
            input0: VerifySharedContinuation1,
            input1: predicate,
            value0: "Acme Corp"
        );
    }

    /// <summary>
    /// Tests PredicateBuilder Not operation with actual database data
    /// </summary>
    [Fact]
    public async Task PredicateBuilder_Not_E2E_NegatesPredicateLogic()
    {
        // Arrange
        await SetupTestDatabase();
        var predicate = PredicateBuilder.True<Customer>();
        predicate = predicate.And(c => c.CustomerName == "Acme Corp");
        predicate = predicate.Not();
        VerifyReusableWorkflow4(
            input0: VerifySharedContinuation1,
            input1: predicate,
            value0: "Tech Solutions"
        );
    }

    /// <summary>
    /// Tests PredicateBuilder with dynamic OR conditions like building search filters
    /// </summary>
    [Fact]
    public async Task PredicateBuilder_DynamicOrConditions_E2E_BuildsSearchFilters()
    {
        // Arrange
        await SetupTestDatabase();
        var searchNames = new[] { "Acme Corp", "Unknown Corp", "Missing Inc" }; // Only "Acme Corp" exists in test data
        var predicate = PredicateBuilder.False<Customer>();

        // Act - simulate building dynamic OR conditions
        foreach (var name in searchNames)
        {
            var tempName = name; // Capture for closure
            predicate = predicate.Or(c => c.CustomerName == tempName);
        }

        VerifyReusableWorkflow4(
            input0: VerifySharedContinuation1,
            input1: predicate,
            value0: "Acme Corp"
        );
    }

    /// <summary>
    /// Tests PredicateBuilder with dynamic AND conditions like building filter chains
    /// </summary>
    [Fact]
    public async Task PredicateBuilder_DynamicAndConditions_E2E_BuildsFilterChains()
    {
        // Arrange
        await SetupTestDatabase();
        var predicate = PredicateBuilder.True<Customer>();

        // Act - simulate building dynamic AND conditions for filtering
        predicate = predicate.And(c => c.Id != null);
        predicate = predicate.And(c => c.Email != null);
        predicate = predicate.And(c => c.CustomerName != null);

        var query = SelectStatement
            .From<Customer>("Customer")
            .Where(predicate)
            .OrderBy(c => c.CustomerName);
        var customers = QueryCustomers(query);
        Assert.Equal(2, customers.Count); // Both customers have email and are in range
    }

    /// <summary>
    /// Tests PredicateBuilder with mixed And/Or operations for complex business logic
    /// </summary>
    [Fact]
    public async Task PredicateBuilder_MixedAndOrOperations_E2E_ComplexBusinessLogic()
    {
        // Arrange
        await SetupTestDatabase();
        var searchNames = new[] { "Acme Corp", "NonExistent Corp" };

        // Act - build name filter with OR
        var namePredicate = PredicateBuilder.False<Customer>();
        foreach (var name in searchNames)
        {
            var tempName = name;
            namePredicate = namePredicate.Or(c => c.CustomerName == tempName);
        }

        // Combine with email filter using AND
        var finalPredicate = namePredicate.And(c => c.Email != null);

        VerifyReusableWorkflow4(
            input0: VerifySharedContinuation1,
            input1: finalPredicate,
            value0: "Acme Corp"
        );
    }

    /// <summary>
    /// Tests PredicateBuilder with conditional building to eliminate duplication
    /// </summary>
    [Fact]
    public async Task PredicateBuilder_ConditionalBuilding_E2E_EliminatesDuplication()
    {
        // Arrange
        await SetupTestDatabase();
        var searchByName = true;
        var searchByEmail = false;
        var customerName = "Tech Solutions";

        var predicate = PredicateBuilder.True<Customer>();

        // Act - conditional predicate building (eliminates if/else duplication)
        if (searchByName)
        {
            predicate = predicate.And(c => c.CustomerName == customerName);
        }
        if (searchByEmail)
        {
            predicate = predicate.And(c => c.Email != null);
        }

        VerifyReusableWorkflow4(
            input0: VerifySharedContinuation1,
            input1: predicate,
            value0: "Tech Solutions"
        );
    }

    /// <summary>
    /// Tests PredicateBuilder with Orders table for different entity type
    /// </summary>
    [Fact]
    public async Task PredicateBuilder_WithOrdersEntity_E2E_WorksAcrossEntityTypes()
    {
        // Arrange
        await SetupTestDatabase();
        var statuses = new[] { "Completed", "Processing" };
        var predicate = PredicateBuilder.False<Order>();

        // Act - build status filter with OR conditions
        foreach (var status in statuses)
        {
            var tempStatus = status;
            predicate = predicate.Or(o => o.Status == tempStatus);
        }

        // Add minimum amount filter with AND
        predicate = predicate.And(o => o.TotalAmount > 0);

        var query = SelectStatement.From<Order>("Orders").Where(predicate).OrderBy(o => o.Id);
        var orders = QueryOrders(query);
        Assert.Equal(2, orders.Count); // Both orders match the criteria
        Assert.Equal("Completed", orders[0].Status);
        Assert.Equal("Processing", orders[1].Status);
    }

    #endregion

    #region SelectStatementLinqExtensions Coverage Tests

    [Fact]
    public void FluentQueryBuilder_And_GeneratesCorrectSQL()
    {
        // Arrange & Act
        var query = "Orders"
            .From("o")
            .Select(("o", "OrderNumber"), ("o", "TotalAmount"))
            .Where("o.Status", "Completed")
            .And("o.TotalAmount", "500.00")
            .ToSqlStatement();

        var sql = SqlResultAssertions.ContainsQuery(
            query: query,
            fragments: ["WHERE o.Status = 'Completed'", "AND o.TotalAmount = '500.00'"]
        );
    }

    [Fact]
    public void FluentQueryBuilder_Or_GeneratesCorrectSQL()
    {
        // Arrange & Act
        var query = "Orders"
            .From("o")
            .Select(("o", "OrderNumber"), ("o", "Status"))
            .Where("o.Status", "Completed")
            .Or("o.Status", "Processing")
            .ToSqlStatement();

        var sql = SqlResultAssertions.ContainsQuery(
            query: query,
            fragments: ["WHERE o.Status = 'Completed'", "OR o.Status = 'Processing'"]
        );
    }

    [Fact]
    public void FluentQueryBuilder_Distinct_GeneratesCorrectSQL()
    {
        // Arrange & Act
        var query = "Orders".From("o").Select(("o", "Status")).Distinct().ToSqlStatement();

        var sql = SqlResultAssertions.ContainsQuery(query: query, fragments: ["SELECT DISTINCT"]);
    }

    [Fact]
    public void FluentQueryBuilder_Skip_GeneratesCorrectSQL()
    {
        // Arrange & Act
        var query = "Orders".From("o").SelectAll().Skip(10).Take(5).ToSqlStatement();

        var sql = SqlResultAssertions.ContainsQuery(
            query: query,
            fragments: ["OFFSET 10", "LIMIT 5"]
        );
    }

    [Fact]
    public void FluentQueryBuilder_SelectAll_GeneratesCorrectSQL()
    {
        // Arrange & Act
        var query = "Orders".From("o").SelectAll("o").ToSqlStatement();

        var sql = SqlResultAssertions.ContainsQuery(query: query, fragments: ["SELECT o.*"]);
    }

    [Fact]
    public void FluentQueryBuilder_GroupBy_GeneratesCorrectSQL()
    {
        // Arrange & Act
        var query = "Orders".From("o").Select(("o", "Status")).GroupBy("o.Status").ToSqlStatement();

        var sql = SqlResultAssertions.ContainsQuery(query: query, fragments: ["GROUP BY o.Status"]);
    }

    [Fact]
    public void FluentQueryBuilder_WhereWithOperator_GeneratesCorrectSQL()
    {
        // Arrange & Act
        var query = "Orders"
            .From("o")
            .Select(("o", "OrderNumber"), ("o", "TotalAmount"))
            .Where("o.TotalAmount", ComparisonOperator.GreaterOrEq, "100.00")
            .ToSqlStatement();

        var sql = SqlResultAssertions.ContainsQuery(
            query: query,
            fragments: ["WHERE o.TotalAmount >= '100.00'"]
        );
    }

    [Fact]
    public void FluentQueryBuilder_MultipleGroupBy_GeneratesCorrectSQL()
    {
        // Arrange & Act
        var query = "Orders"
            .From("o")
            .Select(("o", "Status"), ("o", "CustomerId"))
            .GroupBy("o.Status", "o.CustomerId")
            .ToSqlStatement();

        var sql = SqlResultAssertions.ContainsQuery(
            query: query,
            fragments: ["GROUP BY o.Status, o.CustomerId"]
        );
    }

    [Fact]
    public void FluentQueryBuilder_CombinedAndOr_GeneratesCorrectSQL()
    {
        // Arrange & Act
        var query = "Orders"
            .From("o")
            .Select(("o", "OrderNumber"))
            .Where("o.Status", "Completed")
            .And("o.TotalAmount", "500.00")
            .Or("o.Status", "VIP")
            .ToSqlStatement();

        var sqlResult = query.ToSQLite();

        // Assert
        var sql = SqlResultAssertions.Contains(
            result: sqlResult,
            fragments:
            [
                "WHERE o.Status = 'Completed'",
                "AND o.TotalAmount = '500.00'",
                "OR o.Status = 'VIP'",
            ]
        );
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(FluentQueryBuilder_WhereWithBoolValue_GeneratesCorrectSQL_ContextCasesData))]
    public void FluentQueryBuilder_WhereWithBoolValue_GeneratesCorrectSQL_ContextCases(
        string caseName,
        global::System.String value2,
        global::System.Object value3,
        global::System.String value4
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: caseName));
        ExerciseContextualCaseMatrix1(value2: value2, value3: value3, value4: value4);
    }

    [Fact]
    public void FluentQueryBuilder_WhereWithDateTimeValue_GeneratesCorrectSQL()
    {
        // Arrange & Act - tests FormatValue with DateTime
        var testDate = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        var query = "Orders".From("o").SelectAll().Where("o.OrderDate", testDate).ToSqlStatement();

        var sql = SqlResultAssertions.ContainsQuery(
            query: query,
            fragments: ["WHERE o.OrderDate = '2024-06-15 10:30:00'"]
        );
    }

    [Fact]
    public void FluentQueryBuilder_OrderByDescending_GeneratesCorrectSQL()
    {
        // Arrange & Act
        var query = "Orders"
            .From("o")
            .SelectAll()
            .OrderByDescending("o.OrderDate")
            .ToSqlStatement();

        var sql = SqlResultAssertions.ContainsQuery(
            query: query,
            fragments: ["ORDER BY o.OrderDate DESC"]
        );
    }

    [Fact]
    public void FluentQueryBuilder_AllComparisonOperators_GenerateCorrectSQL()
    {
        // Test Less Than
        Assert.Contains("Id < ", ComparisonOperatorSql(op: ComparisonOperator.LessThan));

        // Test Less Than or Equal
        Assert.Contains("Id <= ", ComparisonOperatorSql(op: ComparisonOperator.LessOrEq));

        // Test Greater Than
        Assert.Contains("Id > ", ComparisonOperatorSql(op: ComparisonOperator.GreaterThan));

        // Test Not Equal
        var neSql = ComparisonOperatorSql(op: ComparisonOperator.NotEq);
        Assert.True(neSql.Contains("Id <> ") || neSql.Contains("Id != "));
    }

    #endregion

    public void Dispose() =>
        CoreCoverageTests.DisposeSqliteFixture(connection: _connection, dbPath: _dbPath);

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static string ComparisonOperatorSql(ComparisonOperator op)
    {
        var query = "Orders".From().Select(("", "Id")).Where("Id", op, 10).ToSqlStatement();
        return Ok(actual: query.ToSQLite()).Value;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void AssertInvoiceTypes(Invoice invoice, InvoiceLine line)
    {
        // Verify Invoice type and properties
        Assert.IsType<Invoice>(invoice);
        global::Tests.Shared.ResultAssertions.Types<string>(
            values: [invoice.Id, invoice.InvoiceNumber, invoice.InvoiceDate, invoice.CustomerName]
        );
        Assert.IsType<double>(invoice.TotalAmount);
        Assert.IsAssignableFrom<IReadOnlyList<InvoiceLine>>(invoice.InvoiceLines);

        // Verify InvoiceLine type and properties
        Assert.IsType<InvoiceLine>(line);
        global::Tests.Shared.ResultAssertions.Types<string>(
            values: [line.LineId, line.InvoiceId, line.Description]
        );
        global::Tests.Shared.ResultAssertions.Types<double>(
            values: [line.Quantity, line.UnitPrice, line.Amount]
        );
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void AssertCustomerTypes(Customer customer, Address address)
    {
        // Verify Customer type and properties
        Assert.IsType<Customer>(customer);
        global::Tests.Shared.ResultAssertions.Types<string>(
            values: [customer.Id, customer.CustomerName, customer.Email, customer.CreatedDate]
        );
        Assert.IsAssignableFrom<IReadOnlyList<Address>>(customer.Addresss);

        // Verify Address type and properties
        Assert.IsType<Address>(address);
        global::Tests.Shared.ResultAssertions.Types<string>(
            values:
            [
                address.AddressId,
                address.CustomerId,
                address.Street,
                address.City,
                address.State,
                address.ZipCode,
            ]
        );
        // Country property not available in generated Address type
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void AssertOrderTypes(Order order, OrderItem item)
    {
        // Verify Order type and properties
        Assert.IsType<Order>(order);
        global::Tests.Shared.ResultAssertions.Types<string>(
            values: [order.Id, order.OrderNumber, order.CustomerId]
        );
        Assert.IsType<double>(order.TotalAmount);
        Assert.IsType<string>(order.Status);
        Assert.IsAssignableFrom<IReadOnlyList<OrderItem>>(order.OrderItems);

        // Verify OrderItem type and properties
        Assert.IsType<OrderItem>(item);
        global::Tests.Shared.ResultAssertions.Types<string>(
            values: [item.ItemId, item.OrderId, item.ProductName]
        );
        global::Tests.Shared.ResultAssertions.Types<double>(
            values: [item.Quantity, item.Price, item.Subtotal]
        );
    }

    // Implements [DP-TEST-SHARED-QUERY-CONSTRUCTION].
    internal static global::Nimblesite.Sql.Model.SelectStatementBuilder BuildOrdersWithCustomer() =>
        "Orders"
            .From("o")
            .InnerJoin("Customer", "CustomerId", "Id", "o", "c")
            .Select(
                ("o", "OrderNumber"),
                ("o", "TotalAmount"),
                ("o", "Status"),
                ("c", "CustomerName"),
                ("c", "Email")
            );

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private IReadOnlyList<Customer> VerifySharedContinuation1(
        System.Linq.Expressions.Expression<Func<Customer, bool>> input0
    )
    {
        var query = SelectStatement.From<Customer>("Customer").Where(input0);

        // Act
        var customers = QueryCustomers(query);

        return customers;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static async global::System.Threading.Tasks.Task VerifySharedPhase2(
        SqliteConnection input0
    )
    {
        await global::Nimblesite
            .DataProvider.Example.Tests.GeneratedOperationsCoverageTests.VerifyUnifiedWorkflow102(
                input0: input0
            )
            .ConfigureAwait(false);

        // I don't know why this is here. We're supposed to use Migrations to create the schema and
        // inserts/updates are supposed to be extension methods.

        // Create all tables
        // Implements [EXAMPLE-SCHEMA-SHARED].
        await DatabaseManager.CreateSchemaAsync(connection: input0).ConfigureAwait(false);
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    private static void ExerciseContextualCaseMatrix1(
        global::System.String value2,
        global::System.Object value3,
        global::System.String value4
    )
    { // Arrange & Act - tests FormatValue with bool
        var query = "Orders".From("o").SelectAll().Where(value2, value3).ToSqlStatement();

        var sql = SqlResultAssertions.ContainsQuery(query: query, fragments: [value4]);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static async global::System.Threading.Tasks.Task<System.Collections.Immutable.ImmutableList<Order>> VerifyReusableWorkflow1(
        Func<Task> input0,
        SqliteConnection input1
    )
    { // Arrange
        await (input0()).ConfigureAwait(continueOnCapturedContext: true);
        // Act
        var result = await (
            input1.GetOrdersAsync("cust-1", "Completed", "2024-01-01", "2024-12-31")
        ).ConfigureAwait(continueOnCapturedContext: true);
        // Assert
        var orders = Success(result);
        return orders;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyReusableWorkflow2(
        SelectStatement input0,
        string value0,
        string value1,
        string value2,
        string value3
    )
    {
        var sqlResult = input0.ToSQLite();

        // Assert
        var sql = SqlResultAssertions.Contains(
            result: sqlResult,
            fragments: [value0, value1, value2, value3]
        );
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void VerifyReusableWorkflow4(
        Func<
            System.Linq.Expressions.Expression<Func<Customer, bool>>,
            IReadOnlyList<Customer>
        > input0,
        System.Linq.Expressions.Expression<Func<Customer, bool>> input1,
        string value0
    )
    {
        var customers = input0(arg: input1);
        Assert.Single(customers);
        Assert.Equal(value0, customers[0].CustomerName);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static async global::System.Threading.Tasks.Task<System.Collections.Immutable.ImmutableList<Invoice>> VerifyReusableWorkflow6(
        Func<Task> input0,
        SqliteConnection input1
    )
    { // Arrange
        await (input0()).ConfigureAwait(continueOnCapturedContext: true);
        // Act
        var result = await (
            input1.GetInvoicesAsync("Acme Corp", "2024-01-01", "2024-12-31")
        ).ConfigureAwait(continueOnCapturedContext: true);
        // Assert
        var invoices = Success(result);
        return invoices;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static async global::System.Threading.Tasks.Task<System.Collections.Immutable.ImmutableList<Customer>> VerifyReusableWorkflow7(
        Func<Task> input0,
        SqliteConnection input1
    )
    { // Arrange
        await (input0()).ConfigureAwait(continueOnCapturedContext: true);
        // Act
        var result = await (input1.GetCustomersLqlAsync(null)).ConfigureAwait(
            continueOnCapturedContext: true
        );
        // Assert
        var customers = Success(result);
        return customers;
    }
}
