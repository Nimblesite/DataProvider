using Microsoft.Data.Sqlite;
using Nimblesite.Lql.SQLite;
using Nimblesite.Sql.Model;
using Outcome;

namespace Nimblesite.DataProvider.Tests;

/// <summary>
/// E2E tests: SQL model types (SelectStatementBuilder, WhereCondition, ColumnInfo,
/// ComparisonOperator, JoinGraph, PredicateBuilder) -> ToSQLite -> execute -> verify.
/// </summary>
public sealed class SqlModelE2ETests : IDisposable
{
    private readonly SqliteTestDatabase _db = new("model_e2e");
    private SqliteConnection _connection => _db.Connection;

    public SqlModelE2ETests()
    {
        CreateSchemaAndSeed();
    }

    public void Dispose() => _db.Dispose();

    private void CreateSchemaAndSeed()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE Employees (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Department TEXT NOT NULL,
                Salary REAL NOT NULL,
                YearsExp INTEGER NOT NULL
            );
            CREATE TABLE Departments (
                Name TEXT PRIMARY KEY,
                Budget REAL NOT NULL,
                HeadCount INTEGER NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();

        using var seedCmd = _connection.CreateCommand();
        seedCmd.CommandText = """
            INSERT INTO Employees VALUES ('e1', 'Alice', 'Engineering', 95000, 5);
            INSERT INTO Employees VALUES ('e2', 'Bob', 'Engineering', 105000, 8);
            INSERT INTO Employees VALUES ('e3', 'Charlie', 'Sales', 75000, 3);
            INSERT INTO Employees VALUES ('e4', 'Diana', 'Sales', 85000, 6);
            INSERT INTO Employees VALUES ('e5', 'Eve', 'Marketing', 70000, 2);
            INSERT INTO Employees VALUES ('e6', 'Frank', 'Engineering', 120000, 12);
            INSERT INTO Employees VALUES ('e7', 'Grace', 'Marketing', 80000, 4);
            INSERT INTO Employees VALUES ('e8', 'Hank', 'Sales', 90000, 7);
            INSERT INTO Departments VALUES ('Engineering', 500000, 3);
            INSERT INTO Departments VALUES ('Sales', 300000, 3);
            INSERT INTO Departments VALUES ('Marketing', 200000, 2);
            """;
        seedCmd.ExecuteNonQuery();
    }

    [Fact]
    public void SelectStatementBuilder_ComplexWhereConditions_ExecuteCorrectly()
    {
        // Build: SELECT Name, Salary FROM Employees
        //        WHERE (Department = 'Engineering' AND Salary > 100000)
        //           OR (Department = 'Sales' AND YearsExp > 5)
        var builder = new SelectStatementBuilder();
        builder.AddTable(name: "Employees");
        builder.AddSelectColumn(name: "Name");
        builder.AddSelectColumn(name: "Salary");
        builder.AddSelectColumn(name: "Department");
        builder.AddWhereCondition(WhereCondition.OpenParen());
        builder.AddWhereCondition(
            WhereCondition.Comparison(
                left: ColumnInfo.Named(name: "Department"),
                @operator: ComparisonOperator.Eq,
                right: "'Engineering'"
            )
        );
        builder.AddWhereCondition(WhereCondition.And());
        builder.AddWhereCondition(
            WhereCondition.Comparison(
                left: ColumnInfo.Named(name: "Salary"),
                @operator: ComparisonOperator.GreaterThan,
                right: "100000"
            )
        );
        builder.AddWhereCondition(WhereCondition.CloseParen());
        builder.AddWhereCondition(WhereCondition.Or());
        builder.AddWhereCondition(WhereCondition.OpenParen());
        builder.AddWhereCondition(
            WhereCondition.Comparison(
                left: ColumnInfo.Named(name: "Department"),
                @operator: ComparisonOperator.Eq,
                right: "'Sales'"
            )
        );
        builder.AddWhereCondition(WhereCondition.And());
        builder.AddWhereCondition(
            WhereCondition.Comparison(
                left: ColumnInfo.Named(name: "YearsExp"),
                @operator: ComparisonOperator.GreaterThan,
                right: "5"
            )
        );
        builder.AddWhereCondition(WhereCondition.CloseParen());
        builder.AddOrderBy(column: "Name", direction: "ASC");
        var stmt = builder.Build();

        var sqlOk = Assert.IsType<StringOk>(stmt.ToSQLite());
        var sql = sqlOk.Value;
        Assert.Contains("(", sql);
        Assert.Contains(")", sql);
        Assert.Contains("AND", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OR", sql, StringComparison.OrdinalIgnoreCase);

        var result = _connection.Query<(string, double, string)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetDouble(1), r.GetString(2))
        );
        var ok = Assert.IsType<Result<IReadOnlyList<(string, double, string)>, SqlError>.Ok<
            IReadOnlyList<(string, double, string)>,
            SqlError
        >>(result);
        var rows = ok.Value;

        // Bob (Eng, 105k), Frank (Eng, 120k), Diana (Sales, 6yr), Hank (Sales, 7yr)
        Assert.Equal(4, rows.Count);
        Assert.Contains(rows, r => r.Item1 == "Bob" && r.Item2 == 105000);
        Assert.Contains(rows, r => r.Item1 == "Frank" && r.Item2 == 120000);
        Assert.Contains(rows, r => r.Item1 == "Diana" && r.Item3 == "Sales");
        Assert.Contains(rows, r => r.Item1 == "Hank" && r.Item3 == "Sales");
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
        var ltBuilder = new SelectStatementBuilder();
        ltBuilder.AddTable(name: "Employees");
        ltBuilder.AddSelectColumn(name: "Name");
        ltBuilder.AddWhereCondition(
            WhereCondition.Comparison(
                left: ColumnInfo.Named(name: "Salary"),
                @operator: ComparisonOperator.LessThan,
                right: "80000"
            )
        );
        var ltSqlOk = Assert.IsType<StringOk>(ltBuilder.Build().ToSQLite());
        var ltSql = ltSqlOk.Value;
        var ltResult = _connection.Query<string>(sql: ltSql, mapper: r => r.GetString(0));
        var ltOk = Assert.IsType<Result<IReadOnlyList<string>, SqlError>.Ok<
            IReadOnlyList<string>,
            SqlError
        >>(ltResult);
        var ltRows = ltOk.Value;
        Assert.Equal(2, ltRows.Count); // Charlie (75k), Eve (70k)

        // Test GreaterOrEq
        var geBuilder = new SelectStatementBuilder();
        geBuilder.AddTable(name: "Employees");
        geBuilder.AddSelectColumn(name: "Name");
        geBuilder.AddWhereCondition(
            WhereCondition.Comparison(
                left: ColumnInfo.Named(name: "Salary"),
                @operator: ComparisonOperator.GreaterOrEq,
                right: "95000"
            )
        );
        var geSqlOk = Assert.IsType<StringOk>(geBuilder.Build().ToSQLite());
        var geSql = geSqlOk.Value;
        var geResult = _connection.Query<string>(sql: geSql, mapper: r => r.GetString(0));
        var geOk = Assert.IsType<Result<IReadOnlyList<string>, SqlError>.Ok<
            IReadOnlyList<string>,
            SqlError
        >>(geResult);
        var geRows = geOk.Value;
        Assert.Equal(3, geRows.Count); // Alice (95k), Bob (105k), Frank (120k)

        // Test LIKE
        var likeBuilder = new SelectStatementBuilder();
        likeBuilder.AddTable(name: "Employees");
        likeBuilder.AddSelectColumn(name: "Name");
        likeBuilder.AddWhereCondition(
            WhereCondition.Comparison(
                left: ColumnInfo.Named(name: "Name"),
                @operator: ComparisonOperator.Like,
                right: "'%a%'"
            )
        );
        var likeSqlOk = Assert.IsType<StringOk>(likeBuilder.Build().ToSQLite());
        var likeSql = likeSqlOk.Value;
        var likeResult = _connection.Query<string>(sql: likeSql, mapper: r => r.GetString(0));
        var likeOk = Assert.IsType<Result<IReadOnlyList<string>, SqlError>.Ok<
            IReadOnlyList<string>,
            SqlError
        >>(likeResult);
        var likeRows = likeOk.Value;
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
        var builder = new SelectStatementBuilder();
        builder.AddTable(name: "Employees");
        builder.AddSelectColumn(ColumnInfo.Named(name: "Department"));
        builder.AddSelectColumn(
            ColumnInfo.FromExpression(expression: "COUNT(*)", alias: "emp_count")
        );
        builder.AddGroupBy([ColumnInfo.Named(name: "Department")]);
        builder.AddOrderBy(column: "Department", direction: "ASC");
        var stmt = builder.Build();
        var sqlOk = Assert.IsType<StringOk>(stmt.ToSQLite());
        var sql = sqlOk.Value;

        var result = _connection.Query<(string, long)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetInt64(1))
        );
        var ok = Assert.IsType<Result<IReadOnlyList<(string, long)>, SqlError>.Ok<
            IReadOnlyList<(string, long)>,
            SqlError
        >>(result);
        var rows = ok.Value;
        Assert.Equal(3, rows.Count);
        Assert.Contains(rows, r => r.Item1 == "Engineering" && r.Item2 == 3);
        Assert.Contains(rows, r => r.Item1 == "Sales" && r.Item2 == 3);
        Assert.Contains(rows, r => r.Item1 == "Marketing" && r.Item2 == 2);
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
        var builder = new SelectStatementBuilder();
        builder.AddTable(name: "Employees");
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

        var joinSqlOk = Assert.IsType<StringOk>(stmt.ToSQLite());
        var sql = joinSqlOk.Value;
        Assert.Contains("JOIN", sql, StringComparison.OrdinalIgnoreCase);

        var result = _connection.Query<(string, double)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetDouble(1))
        );
        var joinOk = Assert.IsType<Result<IReadOnlyList<(string, double)>, SqlError>.Ok<
            IReadOnlyList<(string, double)>,
            SqlError
        >>(result);
        var rows = joinOk.Value;
        Assert.Equal(8, rows.Count); // All 8 employees with their department budget
        Assert.Contains(rows, r => r.Item1 == "Alice" && r.Item2 == 500000);
        Assert.Contains(rows, r => r.Item1 == "Charlie" && r.Item2 == 300000);
        Assert.Contains(rows, r => r.Item1 == "Eve" && r.Item2 == 200000);
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
        var builder = new SelectStatementBuilder();
        builder.AddTable(name: "Employees");
        builder.AddSelectColumn(ColumnInfo.Named(name: "Department"));
        builder.AddSelectColumn(
            ColumnInfo.FromExpression(expression: "AVG(Salary)", alias: "AvgSalary")
        );
        builder.AddGroupBy([ColumnInfo.Named(name: "Department")]);
        builder.WithHaving("AVG(Salary) > 80000");
        builder.AddOrderBy(column: "Department", direction: "ASC");
        var stmt = builder.Build();

        Assert.Equal("AVG(Salary) > 80000", stmt.HavingCondition);

        var havingSqlOk = Assert.IsType<StringOk>(stmt.ToSQLite());
        var sql = havingSqlOk.Value;
        Assert.Contains("HAVING", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AVG(Salary) > 80000", sql);

        var result = _connection.Query<(string, double)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetDouble(1))
        );
        var havingOk = Assert.IsType<Result<IReadOnlyList<(string, double)>, SqlError>.Ok<
            IReadOnlyList<(string, double)>,
            SqlError
        >>(result);
        var rows = havingOk.Value;

        // Engineering avg = (95k+105k+120k)/3 = 106.67k > 80k
        // Sales avg = (75k+85k+90k)/3 = 83.33k > 80k
        // Marketing avg = (70k+80k)/2 = 75k < 80k (excluded)
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Item1 == "Engineering");
        Assert.Contains(rows, r => r.Item1 == "Sales");
        Assert.DoesNotContain(rows, r => r.Item1 == "Marketing");
    }

    [Fact]
    public void DatabaseTableRecord_Properties_WorkCorrectly()
    {
        var table = new DatabaseTable
        {
            Name = "TestTable",
            Schema = "dbo",
            Columns =
            [
                new DatabaseColumn
                {
                    Name = "Id",
                    CSharpType = "Guid",
                    SqlType = "TEXT",
                    IsPrimaryKey = true,
                },
                new DatabaseColumn
                {
                    Name = "AutoId",
                    CSharpType = "int",
                    SqlType = "INTEGER",
                    IsIdentity = true,
                },
                new DatabaseColumn
                {
                    Name = "Computed",
                    CSharpType = "string",
                    SqlType = "TEXT",
                    IsComputed = true,
                },
                new DatabaseColumn
                {
                    Name = "Name",
                    CSharpType = "string",
                    SqlType = "TEXT",
                },
                new DatabaseColumn
                {
                    Name = "Email",
                    CSharpType = "string",
                    SqlType = "TEXT",
                    IsNullable = true,
                    MaxLength = 255,
                },
            ],
        };

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
        Assert.Contains(table.UpdateableColumns, c => c.Name == "Name");
        Assert.Contains(table.UpdateableColumns, c => c.Name == "Email");
    }
}
