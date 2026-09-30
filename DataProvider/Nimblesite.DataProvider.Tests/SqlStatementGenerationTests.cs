using System.Reflection;
using Nimblesite.Lql.Postgres;
using Nimblesite.Lql.SQLite;
using Nimblesite.Lql.SqlServer;
using Nimblesite.Sql.Model;

namespace Nimblesite.DataProvider.Tests;

public sealed class SqlStatementGenerationTests
{
    // Implements [DP-SQL-MODEL-DIALECTS]. A missing provider renderer fails this same test.
    private static void AssertDialect(
        SelectStatement statement,
        string provider,
        params string[] fragments
    )
    {
        // Identifier quoting is provider syntax; PostgresSqlModelIdentifierTests pins it.
        var sql = RenderForProvider(statement, provider)
            .Replace("\"", "", StringComparison.Ordinal);
        Assert.NotEmpty(sql);
        foreach (var fragment in fragments)
        {
            Assert.Contains(fragment, sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    internal static string RenderForProvider(SelectStatement statement, string provider) =>
        Assert.IsType<StringOk>(FindRenderer(provider).Invoke(null, [statement])).Value;

    private static MethodInfo FindRenderer(string provider)
    {
        var (extensionType, methodName) = provider switch
        {
            "sqlite" => (typeof(SqlStatementExtensionsSQLite), "ToSQLite"),
            "postgres" => (typeof(SqlStatementExtensionsPostgreSQL), "ToPostgreSql"),
            "sqlserver" => (typeof(SqlStatementExtensionsSqlServer), "ToSqlServer"),
            _ => (typeof(SqlStatementExtensionsSQLite), "UnknownProvider"),
        };
        return Assert.Single(
            extensionType.GetMethods(),
            method =>
                method.Name == methodName
                && method.GetParameters() is [{ ParameterType: var inputType }]
                && inputType == typeof(SelectStatement)
        );
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    public void AllDialects_SimpleSelectFromSingleTable_GeneratesExpectedSql(string provider)
    {
        var stmt = new SelectStatementBuilder()
            .AddSelectColumn("Id")
            .AddSelectColumn("Name")
            .AddTable("Users")
            .Build();

        var result = stmt.ToSQLite();

        Assert.IsType<StringOk>(result);
        var sql = ((StringOk)result).Value;
        Assert.Equal("SELECT Id, Name FROM Users", sql);
        AssertDialect(stmt, provider, "SELECT", "Id", "Name", "FROM Users");
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    public void AllDialects_SelectAllWildcard_WhenNoColumnsSelected_GeneratesStar(string provider)
    {
        var stmt = new SelectStatementBuilder().AddTable("Users").Build();

        var result = stmt.ToSQLite();

        var success = Assert.IsType<StringOk>(result);
        Assert.Equal("SELECT * FROM Users", success.Value);
        AssertDialect(stmt, provider, "SELECT *", "FROM Users");
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    public void AllDialects_WithWhereComparison_FormatsCondition(string provider)
    {
        var where = WhereCondition.Comparison(
            ColumnInfo.Named("Age"),
            ComparisonOperator.GreaterThan,
            "18"
        );
        var stmt = new SelectStatementBuilder()
            .AddSelectColumn("Id")
            .AddTable("Users")
            .AddWhereCondition(where)
            .Build();

        var success = Assert.IsType<StringOk>(stmt.ToSQLite());
        Assert.Equal("SELECT Id FROM Users WHERE Age > 18", success.Value);
        AssertDialect(stmt, provider, "SELECT Id", "FROM Users", "WHERE", "Age > 18");
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    public void AllDialects_WithWhereLogicalOperators_FormatsSequence(string provider)
    {
        var stmt = new SelectStatementBuilder()
            .AddSelectColumn("Id")
            .AddTable("Users")
            .AddWhereCondition(
                WhereCondition.Comparison(
                    ColumnInfo.Named("Age"),
                    ComparisonOperator.GreaterOrEq,
                    "18"
                )
            )
            .AddWhereCondition(WhereCondition.And())
            .AddWhereCondition(
                WhereCondition.Comparison(
                    ColumnInfo.Named("Country"),
                    ComparisonOperator.Eq,
                    "'AU'"
                )
            )
            .Build();

        var success = Assert.IsType<StringOk>(stmt.ToSQLite());
        Assert.Equal("SELECT Id FROM Users WHERE Age >= 18 AND Country = 'AU'", success.Value);
        AssertDialect(stmt, provider, "WHERE", "Age >= 18", "AND", "Country = 'AU'");
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    public void AllDialects_WithParenthesesInWhere_FormatsParens(string provider)
    {
        var stmt = new SelectStatementBuilder()
            .AddSelectColumn("Id")
            .AddTable("Users")
            .AddWhereCondition(WhereCondition.OpenParen())
            .AddWhereCondition(
                WhereCondition.Comparison(
                    ColumnInfo.Named("Age"),
                    ComparisonOperator.GreaterOrEq,
                    "18"
                )
            )
            .AddWhereCondition(WhereCondition.And())
            .AddWhereCondition(
                WhereCondition.Comparison(
                    ColumnInfo.Named("Age"),
                    ComparisonOperator.LessThan,
                    "65"
                )
            )
            .AddWhereCondition(WhereCondition.CloseParen())
            .AddWhereCondition(WhereCondition.And())
            .AddWhereCondition(
                WhereCondition.Comparison(ColumnInfo.Named("Active"), ComparisonOperator.Eq, "1")
            )
            .Build();

        var success = Assert.IsType<StringOk>(stmt.ToSQLite());
        Assert.Equal(
            "SELECT Id FROM Users WHERE ( Age >= 18 AND Age < 65 ) AND Active = 1",
            success.Value
        );
        AssertDialect(stmt, provider, "WHERE", "Age >= 18", "Age < 65", "AND Active = 1");
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    public void AllDialects_WithJoin_OutputsInnerJoin(string provider)
    {
        var stmt = new SelectStatementBuilder()
            .AddSelectColumn("Users.Id")
            .AddSelectColumn("Orders.Total")
            .AddTable("Users")
            .AddTable("Orders")
            // IMPORTANT: pass full join type text as expected by generator
            .AddJoin("Users", "Orders", "Users.Id = Orders.UserId", "INNER JOIN")
            .Build();

        var success = Assert.IsType<StringOk>(stmt.ToSQLite());
        Assert.Equal(
            "SELECT Users.Id, Orders.Total FROM Users INNER JOIN Orders ON Users.Id = Orders.UserId",
            success.Value
        );
        AssertDialect(stmt, provider, "FROM Users", "INNER JOIN Orders", "ON", "UserId");
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    public void AllDialects_WithGroupByAndHaving_OutputsClauses(string provider)
    {
        var stmt = new SelectStatementBuilder()
            .AddSelectColumn("Country")
            .AddSelectColumn(ColumnInfo.FromExpression("COUNT(*)", "Total"))
            .AddTable("Users")
            .AddGroupBy([ColumnInfo.Named("Country")])
            .WithHaving("COUNT(*) > 10")
            .Build();

        var success = Assert.IsType<StringOk>(stmt.ToSQLite());
        Assert.Equal(
            "SELECT Country, COUNT(*) AS Total FROM Users GROUP BY Country HAVING COUNT(*) > 10",
            success.Value
        );
        AssertDialect(stmt, provider, "COUNT(*)", "GROUP BY Country", "HAVING", "> 10");
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    public void AllDialects_WithOrderBy_OutputsOrderBy(string provider)
    {
        var stmt = new SelectStatementBuilder()
            .AddSelectColumn("Id")
            .AddTable("Users")
            .AddOrderBy("Name", "ASC")
            .AddOrderBy("Id", "DESC")
            .Build();

        var success = Assert.IsType<StringOk>(stmt.ToSQLite());
        Assert.Equal("SELECT Id FROM Users ORDER BY Name ASC, Id DESC", success.Value);
        AssertDialect(stmt, provider, "ORDER BY", "Name ASC", "Id DESC");
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    public void AllDialects_WithDistinctAndPaging_OutputsDistinctLimitOffset(string provider)
    {
        var stmt = new SelectStatementBuilder()
            .WithDistinct(true)
            .AddSelectColumn("Name")
            .AddTable("Users")
            .WithLimit("5")
            .WithOffset("10")
            .Build();

        var success = Assert.IsType<StringOk>(stmt.ToSQLite());
        Assert.Equal("SELECT DISTINCT Name FROM Users LIMIT 5 OFFSET 10", success.Value);
        AssertDialect(stmt, provider, "SELECT DISTINCT", "Name", "5", "10");
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    public void AllDialects_WithWildcardAndAlias_FormatsCorrectly(string provider)
    {
        var stmt = new SelectStatementBuilder()
            .AddSelectColumn(ColumnInfo.Wildcard("u"))
            .AddTable("Users")
            .Build();

        var success = Assert.IsType<StringOk>(stmt.ToSQLite());
        Assert.Equal("SELECT u.* FROM Users", success.Value);
        AssertDialect(stmt, provider, "SELECT", "u.*", "FROM Users");
    }
}
