// Implements [CI-TEST-SHARED-COMMAND].

using Nimblesite.Lql.SQLite;
using static global::Tests.Shared.RowAssertions;
using static global::Tests.Shared.StringAssertions;

namespace Nimblesite.DataProvider.Tests;

/// <summary>
/// E2E tests: SQL model types (SelectStatementBuilder, WhereCondition, ColumnInfo,
/// ComparisonOperator, JoinGraph, PredicateBuilder) -> ToSQLite -> execute -> verify.
/// </summary>
public sealed partial class SqlModelE2ETests : IDisposable
{
    private readonly SqliteTestDatabase _db = new("model_e2e");
    private SqliteConnection _connection => _db.Connection;

    public SqlModelE2ETests()
    {
        CreateSchemaAndSeed();
    }

    public void Dispose() => _db.Dispose();

    private void CreateSchemaAndSeed() =>
        SqliteSchemaSetup.Apply(
            connection: _connection,
            command => command.CommandText = Data_CreateSchemaAndSeed_1,
            command => command.CommandText = Data_CreateSchemaAndSeed_2
        );

    [Fact]
    public void SelectStatementBuilder_ComplexWhereConditions_ExecuteCorrectly()
    {
        // Build: SELECT Name, Salary FROM Employees
        //        WHERE (Department = 'Engineering' AND Salary > 100000)
        //           OR (Department = 'Sales' AND YearsExp > 5)
        var builder = NewEmployeeBuilder();
        builder.AddSelectColumn(name: "Name");
        builder.AddSelectColumn(name: "Salary");
        builder.AddSelectColumn(name: "Department");
        VerifySharedChecks2(
            input0: builder,
            value1: "'Engineering'",
            value2: "Salary",
            value3: "100000"
        );
        builder.AddWhereCondition(WhereCondition.Or());
        VerifySharedChecks2(input0: builder, value1: "'Sales'", value2: "YearsExp", value3: "5");
        builder.AddOrderBy(column: "Name", direction: "ASC");
        var stmt = builder.Build();

        var sql =
            global::Nimblesite.DataProvider.Tests.QueryBuilderE2ETests.VerifyConsolidatedPhase1(
                input0: stmt
            );
        ContainsAll(actual: sql, expected: ["(", ")"]);
        ContainsAll(
            actual: sql,
            expected: ["AND", "OR"],
            comparison: StringComparison.OrdinalIgnoreCase
        );

        var result = _connection.Query<(string, double, string)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetDouble(1), r.GetString(2))
        );
        var ok = global::Tests.Shared.ResultAssertions.ExactOk(actual: result);
        var rows = ok.Value;

        // Bob (Eng, 105k), Frank (Eng, 120k), Diana (Sales, 6yr), Hank (Sales, 7yr)
        Assert.Equal(4, rows.Count);
        ContainsAll(
            rows: rows,
            projection: r => (r.Item1, r.Item2),
            expected: [("Bob", 105000.0), ("Frank", 120000.0)]
        );
        ContainsAll(
            rows: rows,
            projection: r => (r.Item1, r.Item3),
            expected: [("Diana", "Sales"), ("Hank", "Sales")]
        );
    }

    [Fact]
    public void ComparisonOperators_AllOperators_ProduceCorrectSQL()
    {
        // Test each comparison operator's ToSql output
        Assert.Equal("=", ComparisonOperator.Eq.ToSql());
        Assert.Equal("!=", ComparisonOperator.NotEq.ToSql());
        Assert.Equal(">", ComparisonOperator.GreaterThan.ToSql());
        Assert.Equal("<", ComparisonOperator.LessThan.ToSql());
        Assert.Equal(">=", ComparisonOperator.GreaterOrEq.ToSql());
        Assert.Equal("<=", ComparisonOperator.LessOrEq.ToSql());
        Assert.Equal("LIKE", ComparisonOperator.Like.ToSql());
        Assert.Equal("IN", ComparisonOperator.In.ToSql());
        Assert.Equal("IS NULL", ComparisonOperator.IsNull.ToSql());
        Assert.Equal("IS NOT NULL", ComparisonOperator.IsNotNull.ToSql());

        // Test LessThan in a real query
        var ltRows = QueryEmployeeNames(
            column: "Salary",
            @operator: ComparisonOperator.LessThan,
            right: "80000"
        );
        Assert.Equal(2, ltRows.Count); // Charlie (75k), Eve (70k)

        // Test GreaterOrEq
        var geRows = QueryEmployeeNames(
            column: "Salary",
            @operator: ComparisonOperator.GreaterOrEq,
            right: "95000"
        );
        Assert.Equal(3, geRows.Count); // Alice (95k), Bob (105k), Frank (120k)

        // Test LIKE
        var likeRows = QueryEmployeeNames(
            column: "Name",
            @operator: ComparisonOperator.Like,
            right: "'%a%'"
        );
        // Grace, Diana, Frank, Charlie, Hank all contain 'a'
        Assert.True(likeRows.Count >= 3);
    }

    [Fact]
    public void ColumnInfoFactory_AllTypes_WorkCorrectly()
    {
        // Named column
        var named = ColumnInfo.Named(name: "Salary", tableAlias: "e", alias: "emp_salary");
        var namedCol = Assert.IsType<NamedColumn>(named);
        Assert.Equal("Salary", namedCol.Name);
        Assert.Equal("e", namedCol.TableAlias);
        Assert.Equal("emp_salary", namedCol.Alias);

        // Wildcard column
        var wildcard = ColumnInfo.Wildcard(tableAlias: "e");
        var wildcardCol = Assert.IsType<WildcardColumn>(wildcard);
        Assert.Equal("e", wildcardCol.TableAlias);

        // Wildcard without table alias
        var wildcardAll = ColumnInfo.Wildcard();
        var wildcardAllCol = Assert.IsType<WildcardColumn>(wildcardAll);
        Assert.Null(wildcardAllCol.TableAlias);

        // Expression column
        var expr = ColumnInfo.FromExpression(expression: "COUNT(*)", alias: "total_count");
        var exprCol = Assert.IsType<ExpressionColumn>(expr);
        Assert.Equal("COUNT(*)", exprCol.Expression);
        Assert.Equal("total_count", expr.Alias);

        // Use expression column in a real query
        var builder = NewEmployeeBuilder();
        builder.AddSelectColumn(ColumnInfo.Named(name: "Department"));
        builder.AddSelectColumn(
            ColumnInfo.FromExpression(expression: "COUNT(*)", alias: "emp_count")
        );
        builder.AddGroupBy([ColumnInfo.Named(name: "Department")]);
        builder.AddOrderBy(column: "Department", direction: "ASC");
        var stmt = builder.Build();
        var sql =
            global::Nimblesite.DataProvider.Tests.QueryBuilderE2ETests.VerifyConsolidatedPhase1(
                input0: stmt
            );

        var rows = QueryRows<(string, long)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetInt64(1))
        );
        Assert.Equal(3, rows.Count);
        ContainsAll(
            rows: rows,
            projection: r => (r.Item1, r.Item2),
            expected: [("Engineering", 3L), ("Sales", 3L), ("Marketing", 2L)]
        );
    }

    [Fact]
    public void JoinGraph_BuildAndQuery_WorksCorrectly()
    {
        var graph = new JoinGraph();
        graph.Add(
            leftTable: "Employees",
            rightTable: "Departments",
            condition: "Employees.Department = Departments.Name",
            joinType: "INNER"
        );
        Assert.Equal(1, graph.Count);

        var relationships = graph.GetRelationships();
        Assert.Single(relationships);
        Assert.Equal("Employees", relationships[0].LeftTable);
        Assert.Equal("Departments", relationships[0].RightTable);
        Assert.Equal("INNER", relationships[0].JoinType);

        // Build a statement with the JoinGraph
        var builder = NewEmployeeBuilder();
        builder.AddTable(name: "Departments");
        builder.AddJoin(
            leftTable: "Employees",
            rightTable: "Departments",
            condition: "Employees.Department = Departments.Name"
        );
        builder.AddSelectColumn(ColumnInfo.Named(name: "Name", tableAlias: "Employees"));
        builder.AddSelectColumn(ColumnInfo.Named(name: "Budget", tableAlias: "Departments"));
        builder.AddOrderBy(column: "Employees.Name", direction: "ASC");
        var stmt = builder.Build();

        Assert.True(stmt.HasJoins);
        Assert.Equal(2, stmt.Tables.Count);

        var sql =
            global::Nimblesite.DataProvider.Tests.QueryBuilderE2ETests.VerifyConsolidatedPhase1(
                input0: stmt
            );
        Assert.Contains("JOIN", sql, StringComparison.OrdinalIgnoreCase);

        var rows = QueryRows<(string, double)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetDouble(1))
        );
        Assert.Equal(8, rows.Count); // All 8 employees with their department budget
        ContainsAll(
            rows: rows,
            projection: r => (r.Item1, r.Item2),
            expected: [("Alice", 500000.0), ("Charlie", 300000.0), ("Eve", 200000.0)]
        );
    }

    [Fact]
    public void SqlError_FactoryMethods_CreateCorrectErrors()
    {
        // Simple error
        var simple = SqlError.Create("Something went wrong");
        Assert.Equal("Something went wrong", simple.Message);
        Assert.Null(simple.Exception);
        Assert.Null(simple.Position);

        // Error with code
        var coded = SqlError.Create("DB error", errorCode: 42);
        Assert.Equal("DB error", coded.Message);
        Assert.Equal(42, coded.ErrorCode);

        // Error with position
        var positioned = SqlError.WithPosition(
            message: "Syntax error",
            line: 5,
            column: 10,
            source: "SELECT * FORM users"
        );
        Assert.Equal("Syntax error", positioned.Message);
        Assert.NotNull(positioned.Position);
        if (positioned is { Position: { } pos })
        {
            Assert.Equal(5, pos.Line);
            Assert.Equal(10, pos.Column);
        }

        Assert.Equal("SELECT * FORM users", positioned.Source);
        Assert.Contains("5", positioned.FormattedMessage);

        // Error with detailed position
        var detailed = SqlError.WithDetailedPosition(
            message: "Unknown token",
            line: 3,
            column: 15,
            startIndex: 30,
            stopIndex: 35,
            source: "query text here"
        );
        if (detailed is { Position: { } detailedPos })
        {
            Assert.Equal(3, detailedPos.Line);
            Assert.Equal(15, detailedPos.Column);
            Assert.Equal(30, detailedPos.StartIndex);
            Assert.Equal(35, detailedPos.StopIndex);
        }

        // Error from exception
        var ex = new InvalidOperationException("test exception");
        var fromEx = SqlError.FromException(ex);
        Assert.Contains("test exception", fromEx.Message);
        Assert.Equal(ex, fromEx.Exception);

        // Deconstruct - use pattern matching
        if (fromEx is { Message: var message, Exception: var exception })
        {
            Assert.Contains("test exception", message);
            Assert.Equal(ex, exception);
        }
    }

    [Fact]
    public void WhereConditionFactory_AllTypes_WorkCorrectly()
    {
        // Comparison
        var comparison = WhereCondition.Comparison(
            left: ColumnInfo.Named(name: "Age"),
            @operator: ComparisonOperator.GreaterThan,
            right: "30"
        );
        var comp = Assert.IsType<ComparisonCondition>(comparison);
        Assert.Equal("30", comp.Right);

        // Logical operators
        var and = WhereCondition.And();
        Assert.IsAssignableFrom<LogicalOperator>(and);
        var andOp = Assert.IsAssignableFrom<LogicalOperator>(and);
        Assert.Equal("AND", andOp.ToSql());

        var or = WhereCondition.Or();
        Assert.IsAssignableFrom<LogicalOperator>(or);
        var orOp = Assert.IsAssignableFrom<LogicalOperator>(or);
        Assert.Equal("OR", orOp.ToSql());

        // Parentheses
        var open = WhereCondition.OpenParen();
        var openParen = Assert.IsType<Parenthesis>(open);
        Assert.True(openParen.IsOpening);

        var close = WhereCondition.CloseParen();
        var closeParen = Assert.IsType<Parenthesis>(close);
        Assert.False(closeParen.IsOpening);

        // Expression
        var expr = WhereCondition.FromExpression("1 = 1");
        var exprCond = Assert.IsType<ExpressionCondition>(expr);
        Assert.Equal("1 = 1", exprCond.Expression);
    }

    [Fact]
    public void SelectStatementBuilder_HavingClause_ExecutesCorrectly()
    {
        var builder = NewEmployeeBuilder();
        builder.AddSelectColumn(ColumnInfo.Named(name: "Department"));
        builder.AddSelectColumn(
            ColumnInfo.FromExpression(expression: "AVG(Salary)", alias: "AvgSalary")
        );
        builder.AddGroupBy([ColumnInfo.Named(name: "Department")]);
        builder.WithHaving("AVG(Salary) > 80000");
        builder.AddOrderBy(column: "Department", direction: "ASC");
        var stmt = builder.Build();

        Assert.Equal("AVG(Salary) > 80000", stmt.HavingCondition);

        var sql =
            global::Nimblesite.DataProvider.Tests.QueryBuilderE2ETests.VerifyConsolidatedPhase1(
                input0: stmt
            );
        global::Nimblesite.DataProvider.Tests.QueryBuilderE2ETests.VerifyReusableWorkflow1(
            input0: sql,
            value0: "HAVING",
            value2: "AVG(Salary) > 80000"
        );

        var rows = global::Tests.Shared.ResultAssertions.Rows(
            read: () =>
                _connection.Query<(string, double)>(
                    sql: sql,
                    mapper: r => (r.GetString(0), r.GetDouble(1))
                ),
            exactType: true
        );

        // Engineering avg = (95k+105k+120k)/3 = 106.67k > 80k
        // Sales avg = (75k+85k+90k)/3 = 83.33k > 80k
        // Marketing avg = (70k+80k)/2 = 75k < 80k (excluded)
        Assert.Equal(2, rows.Count);
        ContainsAll(rows: rows, projection: r => r.Item1, expected: ["Engineering", "Sales"]);
        Assert.DoesNotContain(rows, r => r.Item1 == "Marketing");
    }

    [Fact]
    public void DatabaseTableRecord_Properties_WorkCorrectly()
    {
        var table = Dataset_DatabaseTableRecord_Properties_WorkCorrectly_1();

        Assert.Equal("TestTable", table.Name);
        Assert.Equal("dbo", table.Schema);
        Assert.Equal(5, table.Columns.Count);

        // PrimaryKeyColumns
        Assert.Single(table.PrimaryKeyColumns);
        Assert.Equal("Id", table.PrimaryKeyColumns[0].Name);

        // InsertableColumns (excludes identity and computed)
        Assert.Equal(3, table.InsertableColumns.Count);
        Assert.DoesNotContain(table.InsertableColumns, c => c.Name == "AutoId");
        Assert.DoesNotContain(table.InsertableColumns, c => c.Name == "Computed");

        // UpdateableColumns (excludes PK, identity, computed)
        Assert.Equal(2, table.UpdateableColumns.Count);
        ContainsAll(
            rows: table.UpdateableColumns,
            projection: c => c.Name,
            expected: ["Name", "Email"]
        );
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void VerifySharedChecks2(
        SelectStatementBuilder input0,
        string value1,
        string value2,
        string value3
    )
    {
        input0.AddWhereCondition(WhereCondition.OpenParen());
        input0.AddWhereCondition(
            WhereCondition.Comparison(
                left: ColumnInfo.Named(name: "Department"),
                @operator: ComparisonOperator.Eq,
                right: value1
            )
        );
        input0.AddWhereCondition(WhereCondition.And());
        input0.AddWhereCondition(
            WhereCondition.Comparison(
                left: ColumnInfo.Named(name: value2),
                @operator: ComparisonOperator.GreaterThan,
                right: value3
            )
        );
        input0.AddWhereCondition(WhereCondition.CloseParen());
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private IReadOnlyList<T> QueryRows<T>(string sql, Func<System.Data.IDataReader, T> mapper) =>
        global::Tests
            .Shared.ResultAssertions.ExactOk(actual: _connection.Query<T>(sql: sql, mapper: mapper))
            .Value;

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static SelectStatementBuilder NewEmployeeBuilder() =>
        global::Nimblesite.DataProvider.Tests.QueryBuilderE2ETests.VerifyReusableWorkflow4(
            value0: "Employees"
        );

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private IReadOnlyList<string> QueryEmployeeNames(
        string column,
        ComparisonOperator @operator,
        string right
    )
    {
        var builder = NewEmployeeBuilder();
        builder.AddSelectColumn(name: "Name");
        builder.AddWhereCondition(
            WhereCondition.Comparison(
                left: ColumnInfo.Named(name: column),
                @operator: @operator,
                right: right
            )
        );
        return VerifySharedChecks5(input0: builder);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private IReadOnlyList<string> VerifySharedChecks5(SelectStatementBuilder input0)
    {
        var ltSqlOk = global::Tests.Shared.ResultAssertions.ExactOk(
            actual: input0.Build().ToSQLite()
        );
        var ltSql = ltSqlOk.Value;
        var ltRows = global::Tests.Shared.ResultAssertions.Rows(
            read: () => _connection.Query<string>(sql: ltSql, mapper: r => r.GetString(0)),
            exactType: true
        );

        return ltRows;
    }
}
