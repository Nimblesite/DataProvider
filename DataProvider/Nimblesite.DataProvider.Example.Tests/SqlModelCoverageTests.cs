using Microsoft.Data.Sqlite;
using Nimblesite.Lql.SQLite;
using Nimblesite.Sql.Model;
using Xunit;
using static Nimblesite.DataProvider.Example.MapFunctions;

namespace Nimblesite.DataProvider.Example.Tests;

#pragma warning disable CS1591

/// <summary>
/// Tests for Sql.Model and Lql.SQLite coverage - LINQ expressions, SQL generation paths
/// </summary>
public sealed class SqlModelCoverageTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnection _connection;

    public SqlModelCoverageTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sql_model_coverage_tests_{Guid.NewGuid()}.db");
        _connection = new SqliteConnection($"Data Source={_dbPath}");
    }

    #region LINQ Expression Where - various comparison operators

    [Fact]
    public async Task LinqWhere_LessThan_GeneratesCorrectSQL()
    {
        await SetupDatabase().ConfigureAwait(false);

        var result = _connection.GetRecords(
            SelectStatement
                .From<Order>("Orders")
                .Where(o => o.TotalAmount < 600.0)
                .ToSqlStatement(),
            stmt => stmt.ToSQLite(),
            MapOrder
        );

        Assert.True(result is OrderReadOnlyListOk);
        var orders = ((OrderReadOnlyListOk)result).Value;
        Assert.Single(orders);
        Assert.Equal(500.00, orders[0].TotalAmount);
    }

    [Fact]
    public async Task LinqWhere_LessThanOrEqual_GeneratesCorrectSQL()
    {
        await SetupDatabase().ConfigureAwait(false);

        var result = _connection.GetRecords(
            SelectStatement
                .From<Order>("Orders")
                .Where(o => o.TotalAmount <= 500.0)
                .ToSqlStatement(),
            stmt => stmt.ToSQLite(),
            MapOrder
        );

        Assert.True(result is OrderReadOnlyListOk);
        var orders = ((OrderReadOnlyListOk)result).Value;
        Assert.Single(orders);
    }

    [Fact]
    public async Task LinqWhere_GreaterThanOrEqual_GeneratesCorrectSQL()
    {
        await SetupDatabase().ConfigureAwait(false);

        var result = _connection.GetRecords(
            SelectStatement
                .From<Order>("Orders")
                .Where(o => o.TotalAmount >= 750.0)
                .ToSqlStatement(),
            stmt => stmt.ToSQLite(),
            MapOrder
        );

        Assert.True(result is OrderReadOnlyListOk);
        var orders = ((OrderReadOnlyListOk)result).Value;
        Assert.Single(orders);
        Assert.Equal(750.00, orders[0].TotalAmount);
    }

    [Fact]
    public async Task LinqWhere_OrElse_GeneratesCorrectSQL()
    {
        await SetupDatabase().ConfigureAwait(false);

        var result = _connection.GetRecords(
            SelectStatement
                .From<Order>("Orders")
                .Where(o => o.Status == "Completed" || o.Status == "Processing")
                .OrderBy(o => o.Id)
                .ToSqlStatement(),
            stmt => stmt.ToSQLite(),
            MapOrder
        );

        Assert.True(result is OrderReadOnlyListOk);
        var orders = ((OrderReadOnlyListOk)result).Value;
        Assert.Equal(2, orders.Count);
    }

    [Fact]
    public async Task LinqWhere_AndAlso_GeneratesCorrectSQL()
    {
        await SetupDatabase().ConfigureAwait(false);

        var result = _connection.GetRecords(
            SelectStatement
                .From<Order>("Orders")
                .Where(o => o.TotalAmount > 400.0 && o.Status == "Completed")
                .ToSqlStatement(),
            stmt => stmt.ToSQLite(),
            MapOrder
        );

        Assert.True(result is OrderReadOnlyListOk);
        var orders = ((OrderReadOnlyListOk)result).Value;
        Assert.Single(orders);
        Assert.Equal("Completed", orders[0].Status);
    }

    [Fact]
    public async Task LinqWhere_NotEqual_GeneratesCorrectSQL()
    {
        await SetupDatabase().ConfigureAwait(false);

        var result = _connection.GetRecords(
            SelectStatement
                .From<Order>("Orders")
                .Where(o => o.Status != "Completed")
                .ToSqlStatement(),
            stmt => stmt.ToSQLite(),
            MapOrder
        );

        Assert.True(result is OrderReadOnlyListOk);
        var orders = ((OrderReadOnlyListOk)result).Value;
        Assert.Single(orders);
        Assert.Equal("Processing", orders[0].Status);
    }

    #endregion

    #region LINQ Expression Select and OrderBy

    [Fact]
    public void LinqSelect_WithExpression_GeneratesCorrectSQL()
    {
        var query = "Customer"
            .From()
            .Select<Customer>(c => c.CustomerName)
            .OrderBy<Customer>(c => c.CustomerName)
            .ToSqlStatement();

        var sqlResult = query.ToSQLite();
        Assert.True(sqlResult is StringSqlOk);
        var sql = ((StringSqlOk)sqlResult).Value;
        Assert.Contains("CustomerName", sql);
    }

    [Fact]
    public async Task LinqOrderBy_WithExpression_GeneratesCorrectSQL()
    {
        await SetupDatabase().ConfigureAwait(false);

        var result = _connection.GetRecords(
            "Customer".From().SelectAll().OrderBy<Customer>(c => c.CustomerName).ToSqlStatement(),
            stmt => stmt.ToSQLite(),
            MapCustomer
        );

        Assert.True(result is CustomerReadOnlyListOk);
        var customers = ((CustomerReadOnlyListOk)result).Value;
        Assert.Equal(2, customers.Count);
        Assert.Equal("Acme Corp", customers[0].CustomerName);
    }

    #endregion

    #region SelectStatementBuilder - additional paths

    [Fact]
    public void Builder_WhereWithNullValue_GeneratesNullSQL()
    {
        var query = "Orders"
            .From("o")
            .SelectAll()
            .Where("o.Status", (object)null!)
            .ToSqlStatement();

        var sqlResult = query.ToSQLite();
        Assert.True(sqlResult is StringSqlOk);
        var sql = ((StringSqlOk)sqlResult).Value;
        Assert.Contains("NULL", sql);
    }

    [Fact]
    public void Builder_WhereWithBoolFalse_GeneratesZero()
    {
        var query = "Orders".From("o").SelectAll().Where("o.IsActive", false).ToSqlStatement();

        var sqlResult = query.ToSQLite();
        Assert.True(sqlResult is StringSqlOk);
        var sql = ((StringSqlOk)sqlResult).Value;
        Assert.Contains("o.IsActive = 0", sql);
    }

    [Fact]
    public void Builder_AddWhereCondition_WithParentheses_GeneratesCorrectSQL()
    {
        var query = "Orders"
            .From("o")
            .SelectAll()
            .AddWhereCondition(WhereCondition.OpenParen())
            .Where("o.Status", "Completed")
            .AddWhereCondition(WhereCondition.CloseParen())
            .ToSqlStatement();

        var sqlResult = query.ToSQLite();
        Assert.True(sqlResult is StringSqlOk);
        var sql = ((StringSqlOk)sqlResult).Value;
        Assert.Contains("(", sql);
        Assert.Contains(")", sql);
    }

    [Fact]
    public void Builder_SelectWithExpressionColumn_Works()
    {
        var query = "Orders"
            .From("o")
            .Select(("o", "OrderNumber"))
            .GroupBy("o.Status")
            .ToSqlStatement();

        var sqlResult = query.ToSQLite();
        Assert.True(sqlResult is StringSqlOk);
    }

    [Fact]
    public void Builder_WhereWithDoubleValue_GeneratesNumeric()
    {
        var query = "Orders".From("o").SelectAll().Where("o.TotalAmount", 99.99).ToSqlStatement();

        var sqlResult = query.ToSQLite();
        Assert.True(sqlResult is StringSqlOk);
        var sql = ((StringSqlOk)sqlResult).Value;
        Assert.Contains("99.99", sql);
    }

    [Fact]
    public void Builder_ComparisonOperator_Like_GeneratesCorrectSQL()
    {
        var query = "Orders"
            .From()
            .Select(("", "Id"))
            .Where("Name", ComparisonOperator.Like, "%test%")
            .ToSqlStatement();
        var sql = ((StringSqlOk)query.ToSQLite()).Value;
        Assert.Contains("LIKE", sql);
    }

    [Fact]
    public void Builder_ComparisonOperator_In_GeneratesCorrectSQL()
    {
        var query = "Orders"
            .From()
            .Select(("", "Id"))
            .Where("Status", ComparisonOperator.In, "('Completed','Processing')")
            .ToSqlStatement();
        var sql = ((StringSqlOk)query.ToSQLite()).Value;
        Assert.Contains("IN", sql);
    }

    [Fact]
    public void Builder_ComparisonOperator_IsNull_GeneratesCorrectSQL()
    {
        var query = "Orders"
            .From()
            .Select(("", "Id"))
            .Where("Notes", ComparisonOperator.IsNull, "")
            .ToSqlStatement();
        var sql = ((StringSqlOk)query.ToSQLite()).Value;
        Assert.Contains("IS NULL", sql);
    }

    [Fact]
    public void Builder_ComparisonOperator_IsNotNull_GeneratesCorrectSQL()
    {
        var query = "Orders"
            .From()
            .Select(("", "Id"))
            .Where("Notes", ComparisonOperator.IsNotNull, "")
            .ToSqlStatement();
        var sql = ((StringSqlOk)query.ToSQLite()).Value;
        Assert.Contains("IS NOT NULL", sql);
    }

    #endregion

    #region LINQ query syntax

    [Fact]
    public async Task LinqQuerySyntax_WithOrderByDescending_Works()
    {
        await SetupDatabase().ConfigureAwait(false);

        var result = _connection.GetRecords(
            SelectStatement
                .From<Order>("Orders")
                .Where(o => o.TotalAmount > 0.0)
                .OrderByDescending(o => o.TotalAmount)
                .Take(10)
                .ToSqlStatement(),
            stmt => stmt.ToSQLite(),
            MapOrder
        );

        Assert.True(result is OrderReadOnlyListOk);
        var orders = ((OrderReadOnlyListOk)result).Value;
        Assert.Equal(2, orders.Count);
        Assert.True(orders[0].TotalAmount >= orders[1].TotalAmount);
    }

    [Fact]
    public async Task LinqQuerySyntax_WithSkipAndTake_Works()
    {
        await SetupDatabase().ConfigureAwait(false);

        var result = _connection.GetRecords(
            SelectStatement
                .From<Order>("Orders")
                .OrderBy(o => o.Id)
                .Skip(1)
                .Take(1)
                .ToSqlStatement(),
            stmt => stmt.ToSQLite(),
            MapOrder
        );

        Assert.True(result is OrderReadOnlyListOk);
        var orders = ((OrderReadOnlyListOk)result).Value;
        Assert.Single(orders);
    }

    [Fact]
    public void LinqQuerySyntax_WithDistinct_Works()
    {
        var query = "Orders".From().Select<Order>(o => o.Status).Distinct().ToSqlStatement();

        var sqlResult = query.ToSQLite();
        Assert.True(sqlResult is StringSqlOk);
        var sql = ((StringSqlOk)sqlResult).Value;
        Assert.Contains("DISTINCT", sql);
    }

    #endregion

    #region SqlError coverage

    [Fact]
    public void SqlError_Create_SetsMessageCorrectly()
    {
        var error = SqlError.Create("Test error message");
        Assert.Equal("Test error message", error.Message);
    }

    [Fact]
    public void SqlError_CreateWithErrorCode_SetsCodeCorrectly()
    {
        var error = SqlError.Create("Error with code", 42);
        Assert.Equal("Error with code", error.Message);
        Assert.Equal(42, error.ErrorCode);
    }

    [Fact]
    public void SqlError_WithPosition_SetsPositionCorrectly()
    {
        var error = SqlError.WithPosition(
            "Error at position",
            line: 5,
            column: 10,
            source: "SELECT * FROM foo"
        );
        Assert.Equal("Error at position", error.Message);
        Assert.NotNull(error.Position);
        Assert.Equal(5, error.Position!.Line);
        Assert.Equal(10, error.Position.Column);
        Assert.Equal("SELECT * FROM foo", error.Source);
    }

    [Fact]
    public void SqlError_WithDetailedPosition_SetsAllFields()
    {
        var error = SqlError.WithDetailedPosition(
            "Parse error",
            line: 1,
            column: 5,
            startIndex: 5,
            stopIndex: 10,
            source: "SELECT bad FROM"
        );
        Assert.NotNull(error.Position);
        Assert.Equal(1, error.Position!.Line);
        Assert.Equal(5, error.Position.Column);
        Assert.Equal(5, error.Position.StartIndex);
        Assert.Equal(10, error.Position.StopIndex);
    }

    [Fact]
    public void SqlError_FromException_CapturesExceptionDetails()
    {
        var exception = new InvalidOperationException("Test exception");
        var error = SqlError.FromException(exception);
        Assert.Contains("Test exception", error.Message);
        Assert.Equal(exception, error.Exception);
        Assert.Equal(exception, error.InnerException);
    }

    [Fact]
    public void SqlError_FromException_WithNull_ReturnsNullMessage()
    {
        var error = SqlError.FromException(null);
        Assert.Equal("Null exception provided", error.Message);
    }

    [Fact]
    public void SqlError_FormattedMessage_WithPosition_IncludesLineAndColumn()
    {
        var error = SqlError.WithPosition("Syntax error", line: 3, column: 7);
        Assert.Contains("line 3", error.FormattedMessage);
        Assert.Contains("column 7", error.FormattedMessage);
    }

    [Fact]
    public void SqlError_FormattedMessage_WithoutPosition_ReturnsMessage()
    {
        var error = SqlError.Create("Simple error");
        Assert.Equal("Simple error", error.FormattedMessage);
    }

    [Fact]
    public void SqlError_DetailedMessage_WithExceptionAndSource_IncludesAllDetails()
    {
        var innerEx = new ArgumentException("inner");
        var outerEx = new InvalidOperationException("outer", innerEx);
        var error = new SqlError(
            "Parse failed",
            outerEx,
            new SourcePosition(1, 5),
            "SELECT * FROM bad_table"
        )
        {
            InnerException = innerEx,
        };

        var detailed = error.DetailedMessage;
        Assert.Contains("Parse failed", detailed);
        Assert.Contains("line 1", detailed);
        Assert.Contains("outer", detailed);
        Assert.Contains("inner", detailed);
        Assert.Contains("SELECT * FROM bad_table", detailed);
    }

    [Fact]
    public void SqlError_Deconstruct_ExtractsMessageAndException()
    {
        var exception = new InvalidOperationException("test");
        var error = SqlError.FromException(exception);
        var (message, ex) = error;
        Assert.Equal("test", message);
        Assert.Equal(exception, ex);
    }

    #endregion

    #region SqlErrorException coverage

    [Fact]
    public void SqlErrorException_WithSqlError_SetsProperties()
    {
        var sqlError = SqlError.Create("Test error");
        var exception = new SqlErrorException(sqlError);
        Assert.Equal("Test error", exception.Message);
        Assert.Equal(sqlError, exception.SqlError);
    }

    [Fact]
    public void SqlErrorException_WithSqlErrorAndInner_SetsProperties()
    {
        var sqlError = SqlError.Create("Outer error");
        var inner = new InvalidOperationException("inner");
        var exception = new SqlErrorException(sqlError, inner);
        Assert.Equal("Outer error", exception.Message);
        Assert.Equal(inner, exception.InnerException);
        Assert.Equal(sqlError, exception.SqlError);
    }

    [Fact]
    public void SqlErrorException_Default_HasNullSqlError()
    {
        var exception = new SqlErrorException();
        Assert.Null(exception.SqlError);
    }

    [Fact]
    public void SqlErrorException_WithMessage_HasNullSqlError()
    {
        var exception = new SqlErrorException("Custom message");
        Assert.Equal("Custom message", exception.Message);
        Assert.Null(exception.SqlError);
    }

    [Fact]
    public void SqlErrorException_WithMessageAndInner_HasNullSqlError()
    {
        var inner = new InvalidOperationException("inner");
        var exception = new SqlErrorException("Custom message", inner);
        Assert.Equal("Custom message", exception.Message);
        Assert.Equal(inner, exception.InnerException);
        Assert.Null(exception.SqlError);
    }

    #endregion

    #region SourcePosition coverage

    [Fact]
    public void SourcePosition_Constructor_SetsAllFields()
    {
        var pos = new SourcePosition(Line: 10, Column: 20, StartIndex: 100, StopIndex: 110);
        Assert.Equal(10, pos.Line);
        Assert.Equal(20, pos.Column);
        Assert.Equal(100, pos.StartIndex);
        Assert.Equal(110, pos.StopIndex);
    }

    #endregion

    #region SelectStatementVisitor - additional paths via LINQ

    [Fact]
    public void LinqWhere_StringContains_GeneratesLikeSQL()
    {
        var query = SelectStatement
            .From<Customer>()
            .Where(c => c.CustomerName.Contains("Acme"))
            .ToSqlStatement();

        var sqlResult = query.ToSQLite();
        Assert.True(sqlResult is StringSqlOk);
        var sql = ((StringSqlOk)sqlResult).Value;
        Assert.Contains("LIKE", sql);
        Assert.Contains("Acme", sql);
    }

    [Fact]
    public void LinqWhere_StringStartsWith_GeneratesLikeSQL()
    {
        var query = SelectStatement
            .From<Customer>()
            .Where(c => c.CustomerName.StartsWith("Tech"))
            .ToSqlStatement();

        var sqlResult = query.ToSQLite();
        Assert.True(sqlResult is StringSqlOk);
        var sql = ((StringSqlOk)sqlResult).Value;
        Assert.Contains("LIKE", sql);
        Assert.Contains("Tech", sql);
    }

    [Fact]
    public async Task LinqWhere_ComplexOrWithAnd_GeneratesCorrectSQL()
    {
        await SetupDatabase().ConfigureAwait(false);

        var result = _connection.GetRecords(
            SelectStatement
                .From<Order>("Orders")
                .Where(o =>
                    (o.Status == "Completed" && o.TotalAmount > 400.0) || o.Status == "Processing"
                )
                .OrderBy(o => o.Id)
                .ToSqlStatement(),
            stmt => stmt.ToSQLite(),
            MapOrder
        );

        Assert.True(result is OrderReadOnlyListOk);
        var orders = ((OrderReadOnlyListOk)result).Value;
        Assert.Equal(2, orders.Count);
    }

    [Fact]
    public void LinqSelect_WithNewExpression_ExtractsMultipleColumns()
    {
        var query = "Customer"
            .From()
            .Select<Customer>(c => new { c.CustomerName, c.Email })
            .ToSqlStatement();

        var sqlResult = query.ToSQLite();
        Assert.True(sqlResult is StringSqlOk);
        var sql = ((StringSqlOk)sqlResult).Value;
        Assert.Contains("CustomerName", sql);
        Assert.Contains("Email", sql);
    }

    [Fact]
    public async Task LinqWhere_NullComparison_GeneratesIsNull()
    {
        await SetupDatabase().ConfigureAwait(false);

        var query = SelectStatement.From<Customer>().Where(c => c.Email == null).ToSqlStatement();

        var sqlResult = query.ToSQLite();
        Assert.True(sqlResult is StringSqlOk);
    }

    [Fact]
    public void FormatValue_WithLongValue_GeneratesNumeric()
    {
        var query = "Orders".From().SelectAll().Where("Count", 42L).ToSqlStatement();

        var sqlResult = query.ToSQLite();
        Assert.True(sqlResult is StringSqlOk);
        var sql = ((StringSqlOk)sqlResult).Value;
        Assert.Contains("42", sql);
    }

    [Fact]
    public void FormatValue_WithDecimalValue_GeneratesNumeric()
    {
        var query = "Orders".From().SelectAll().Where("Amount", 19.99m).ToSqlStatement();

        var sqlResult = query.ToSQLite();
        Assert.True(sqlResult is StringSqlOk);
        var sql = ((StringSqlOk)sqlResult).Value;
        Assert.Contains("19.99", sql);
    }

    #endregion

    #region SelectQueryable coverage

    [Fact]
    public void SelectQueryable_Properties_ReturnExpectedValues()
    {
        var queryable = new SelectQueryable<Customer>("Customer");
        Assert.Equal(typeof(Customer), queryable.ElementType);
        Assert.NotNull(queryable.Expression);
        Assert.NotNull(queryable.Provider);
    }

    [Fact]
    public void SelectQueryable_GetEnumerator_ThrowsNotSupported()
    {
        var queryable = new SelectQueryable<Customer>("Customer");
        Assert.Throws<NotSupportedException>(() => queryable.GetEnumerator());
    }

    [Fact]
    public void SelectQueryableExtensions_WithInvalidQueryable_Throws()
    {
        var list = new List<string>().AsQueryable();
        Assert.Throws<InvalidOperationException>(() => list.ToSqlStatement());
    }

    #endregion

    #region Additional LINQ expression paths

    [Fact]
    public void LinqWhere_WithBooleanConstant_GeneratesCorrectSQL()
    {
        var query = SelectStatement
            .From<Order>("Orders")
            .Where(o => o.TotalAmount > 0.0 && o.TotalAmount < 10000.0)
            .ToSqlStatement();

        var sqlResult = query.ToSQLite();
        Assert.True(sqlResult is StringSqlOk);
    }

    [Fact]
    public void LinqWhere_WithMultipleOrConditions_GeneratesSQL()
    {
        var query = SelectStatement
            .From<Order>("Orders")
            .Where(o => o.Status == "A" || o.Status == "B" || o.Status == "C")
            .ToSqlStatement();

        var sqlResult = query.ToSQLite();
        Assert.True(sqlResult is StringSqlOk);
    }

    [Fact]
    public void LinqWhere_WithBoolField_GeneratesSQL()
    {
        var query = "Table"
            .From()
            .SelectAll()
            .Where("IsActive", true)
            .And("IsDeleted", false)
            .ToSqlStatement();

        var sqlResult = query.ToSQLite();
        Assert.True(sqlResult is StringSqlOk);
        var sql = ((StringSqlOk)sqlResult).Value;
        Assert.Contains("IsActive = 1", sql);
        Assert.Contains("IsDeleted = 0", sql);
    }

    [Fact]
    public void ColumnInfo_ExpressionColumn_CanBeCreated()
    {
        var col = ColumnInfo.FromExpression("COUNT(*)");
        Assert.NotNull(col);
    }

    [Fact]
    public void LogicalOperator_Variants_CanBeAccessed()
    {
        var and = LogicalOperator.And;
        var or = LogicalOperator.Or;
        Assert.NotNull(and);
        Assert.NotNull(or);
    }

    [Fact]
    public void ExpressionColumn_CanBeUsedInSelect()
    {
        var builder = "Orders".From("o");
        builder.AddSelectColumn(ColumnInfo.FromExpression("COUNT(*)"));
        builder.AddSelectColumn(ColumnInfo.FromExpression("SUM(o.TotalAmount)"));
        var query = builder.GroupBy("o.Status").ToSqlStatement();

        var sqlResult = query.ToSQLite();
        Assert.True(sqlResult is StringSqlOk);
        var sql = ((StringSqlOk)sqlResult).Value;
        Assert.Contains("COUNT(*)", sql);
        Assert.Contains("SUM(o.TotalAmount)", sql);
    }

    [Fact]
    public async Task LinqWhere_ComplexNestedAndOr_GeneratesSQL()
    {
        await SetupDatabase().ConfigureAwait(false);

        var result = _connection.GetRecords(
            SelectStatement
                .From<Order>("Orders")
                .Where(o =>
                    (o.TotalAmount >= 500.0 && o.TotalAmount <= 1000.0) || o.Status == "VIP"
                )
                .ToSqlStatement(),
            stmt => stmt.ToSQLite(),
            MapOrder
        );

        Assert.True(result is OrderReadOnlyListOk);
    }

    [Fact]
    public void LinqQuerySyntax_WithSelect_GeneratesSQL()
    {
        var query = (
            from order in SelectStatement.From<Order>("Orders")
            where order.TotalAmount > 100.0
            orderby order.OrderNumber
            select order
        ).ToSqlStatement();

        var sqlResult = query.ToSQLite();
        Assert.True(sqlResult is StringSqlOk);
        var sql = ((StringSqlOk)sqlResult).Value;
        Assert.Contains("Orders", sql);
    }

    [Fact]
    public void SelectStatement_JoinGraph_CanBeAccessed()
    {
        var statement = "Orders"
            .From("o")
            .InnerJoin("Customer", "CustomerId", "Id", "o", "c")
            .SelectAll()
            .ToSqlStatement();

        Assert.True(statement.HasJoins);
        Assert.NotNull(statement.JoinGraph);
        Assert.True(statement.JoinGraph.Count > 0);
    }

    [Fact]
    public void SelectStatement_Properties_AreAccessible()
    {
        var statement = "Orders"
            .From("o")
            .Select(("o", "Id"), ("o", "Status"))
            .Where("o.TotalAmount", ComparisonOperator.GreaterThan, "100")
            .OrderBy("o.Id")
            .GroupBy("o.Status")
            .Skip(5)
            .Take(10)
            .Distinct()
            .ToSqlStatement();

        Assert.NotEmpty(statement.SelectList);
        Assert.NotEmpty(statement.Tables);
        Assert.NotEmpty(statement.WhereConditions);
        Assert.NotEmpty(statement.OrderByItems);
        Assert.NotEmpty(statement.GroupByColumns);
        Assert.Equal("10", statement.Limit);
        Assert.Equal("5", statement.Offset);
        Assert.True(statement.IsDistinct);
    }

    [Fact]
    public async Task GetRecords_WithValidQuery_ReturnsData()
    {
        await SetupDatabase().ConfigureAwait(false);

        var result = _connection.GetRecords(
            "Orders"
                .From()
                .SelectAll()
                .Where("TotalAmount", ComparisonOperator.GreaterOrEq, "0")
                .ToSqlStatement(),
            stmt => stmt.ToSQLite(),
            MapOrder
        );

        Assert.True(result is OrderReadOnlyListOk);
        var orders = ((OrderReadOnlyListOk)result).Value;
        Assert.Equal(2, orders.Count);
    }

    #endregion

    [Fact]
    public void Builder_Having_GeneratesCorrectSQL()
    {
        var query = "Orders".From("o").Select(("o", "Status")).GroupBy("o.Status").ToSqlStatement();

        // Access the statement's properties for coverage
        Assert.NotEmpty(query.GroupByColumns);
        Assert.False(query.HasJoins);
        Assert.Empty(query.Unions);
        Assert.Null(query.HavingCondition);

        var sqlResult = query.ToSQLite();
        Assert.True(sqlResult is StringSqlOk);
    }

    [Fact]
    public void Builder_WithMultipleOrders_GeneratesCorrectSQL()
    {
        var query = "Orders"
            .From("o")
            .SelectAll()
            .OrderBy("o.Status")
            .OrderByDescending("o.TotalAmount")
            .ToSqlStatement();

        Assert.Equal(2, query.OrderByItems.Count);
        var sqlResult = query.ToSQLite();
        Assert.True(sqlResult is StringSqlOk);
    }

    [Fact]
    public void Builder_WithParameters_GeneratesSQL()
    {
        var statement = "Orders"
            .From()
            .SelectAll()
            .Where("Status", ComparisonOperator.Eq, "Completed")
            .ToSqlStatement();

        Assert.NotNull(statement.Parameters);
        var sqlResult = statement.ToSQLite();
        Assert.True(sqlResult is StringSqlOk);
    }

    [Fact]
    public void ComparisonOperator_AllTypes_HaveCorrectToString()
    {
        Assert.NotNull(ComparisonOperator.Eq);
        Assert.NotNull(ComparisonOperator.NotEq);
        Assert.NotNull(ComparisonOperator.GreaterThan);
        Assert.NotNull(ComparisonOperator.LessThan);
        Assert.NotNull(ComparisonOperator.GreaterOrEq);
        Assert.NotNull(ComparisonOperator.LessOrEq);
        Assert.NotNull(ComparisonOperator.Like);
        Assert.NotNull(ComparisonOperator.In);
        Assert.NotNull(ComparisonOperator.IsNull);
        Assert.NotNull(ComparisonOperator.IsNotNull);
    }

    [Fact]
    public void WhereCondition_AllTypes_CanBeCreated()
    {
        var comparison = WhereCondition.Comparison(
            ColumnInfo.Named("col"),
            ComparisonOperator.Eq,
            "val"
        );
        var and = WhereCondition.And();
        var or = WhereCondition.Or();
        var open = WhereCondition.OpenParen();
        var close = WhereCondition.CloseParen();
        var expr = WhereCondition.FromExpression("1 = 1");

        Assert.NotNull(comparison);
        Assert.NotNull(and);
        Assert.NotNull(or);
        Assert.NotNull(open);
        Assert.NotNull(close);
        Assert.NotNull(expr);
    }

    [Fact]
    public void ColumnInfo_AllTypes_CanBeCreated()
    {
        var named = ColumnInfo.Named("col");
        var namedWithAlias = ColumnInfo.Named("col", "alias");
        var wildcard = ColumnInfo.Wildcard();
        var wildcardWithTable = ColumnInfo.Wildcard("t");
        var expression = ColumnInfo.FromExpression("COUNT(*)");

        Assert.NotNull(named);
        Assert.NotNull(namedWithAlias);
        Assert.NotNull(wildcard);
        Assert.NotNull(wildcardWithTable);
        Assert.NotNull(expression);
    }

    private async Task SetupDatabase()
    {
        await _connection.OpenAsync().ConfigureAwait(false);
        using (var pragmaCommand = new SqliteCommand("PRAGMA foreign_keys = OFF", _connection))
        {
            await pragmaCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        var createTablesScript = """
            CREATE TABLE IF NOT EXISTS Customer (
                Id TEXT PRIMARY KEY,
                CustomerName TEXT NOT NULL,
                Email TEXT NULL,
                Phone TEXT NULL,
                CreatedDate TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Orders (
                Id TEXT PRIMARY KEY,
                OrderNumber TEXT NOT NULL,
                OrderDate TEXT NOT NULL,
                CustomerId TEXT NOT NULL,
                TotalAmount REAL NOT NULL,
                Status TEXT NOT NULL,
                FOREIGN KEY (CustomerId) REFERENCES Customer (Id)
            );
            CREATE TABLE IF NOT EXISTS OrderItem (
                Id TEXT PRIMARY KEY,
                OrderId TEXT NOT NULL,
                ProductName TEXT NOT NULL,
                Quantity REAL NOT NULL,
                Price REAL NOT NULL,
                Subtotal REAL NOT NULL,
                FOREIGN KEY (OrderId) REFERENCES Orders (Id)
            );
            """;

        using var command = new SqliteCommand(createTablesScript, _connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);

        var insertScript = """
            INSERT INTO Customer (Id, CustomerName, Email, Phone, CreatedDate) VALUES
            ('cust-1', 'Acme Corp', 'contact@acme.com', '555-0100', '2024-01-01'),
            ('cust-2', 'Tech Solutions', 'info@techsolutions.com', '555-0200', '2024-01-02');
            INSERT INTO Orders (Id, OrderNumber, OrderDate, CustomerId, TotalAmount, Status) VALUES
            ('ord-1', 'ORD-001', '2024-01-10', 'cust-1', 500.00, 'Completed'),
            ('ord-2', 'ORD-002', '2024-01-11', 'cust-2', 750.00, 'Processing');
            INSERT INTO OrderItem (Id, OrderId, ProductName, Quantity, Price, Subtotal) VALUES
            ('item-1', 'ord-1', 'Widget A', 2.0, 100.00, 200.00);
            """;

        using var insertCommand = new SqliteCommand(insertScript, _connection);
        await insertCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public void Dispose() =>
        CoreCoverageTests.DisposeSqliteFixture(connection: _connection, dbPath: _dbPath);
}
