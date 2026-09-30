using Microsoft.Data.Sqlite;
using Nimblesite.Lql.Core;
using Nimblesite.Lql.SQLite;
using Nimblesite.Sql.Model;
using Outcome;

namespace Nimblesite.DataProvider.Tests;

public sealed class SQLiteContextE2ETests : IDisposable
{
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(),
        $"ctx_e2e_{Guid.NewGuid()}.db"
    );
    private readonly SqliteConnection _connection;

    public SQLiteContextE2ETests()
    {
        _connection = new SqliteConnection($"Data Source={_dbPath}");
        _connection.Open();
        CreateSchemaAndSeed();
    }

    public void Dispose()
    {
        _connection.Dispose();
        try
        {
            File.Delete(_dbPath);
        }
        catch (IOException)
        { /* cleanup best-effort */
        }
    }

    private static Pipeline WithIdentity(string table)
    {
        var p = new Pipeline();
        p.Steps.Add(new IdentityStep { Base = new Identifier(table) });
        return p;
    }

    private static Identifier Id(string name) => new(name);

    private static Pipeline CountByCountryPipeline()
    {
        var pipeline = WithIdentity("users");
        pipeline.Steps.Add(
            new SelectStep([
                ColumnInfo.Named("country"),
                ColumnInfo.FromExpression("COUNT(*)", alias: "cnt"),
            ])
            {
                Base = Id("users"),
            }
        );
        pipeline.Steps.Add(new GroupByStep(["country"]) { Base = Id("users") });
        return pipeline;
    }

    private void CreateSchemaAndSeed()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE users (
                id TEXT PRIMARY KEY, name TEXT NOT NULL, email TEXT NOT NULL,
                age INTEGER NOT NULL, country TEXT NOT NULL,
                status TEXT NOT NULL DEFAULT 'active'
            );
            CREATE TABLE orders (
                id TEXT PRIMARY KEY, user_id TEXT NOT NULL, total REAL NOT NULL,
                status TEXT NOT NULL DEFAULT 'pending',
                FOREIGN KEY (user_id) REFERENCES users(id)
            );
            CREATE TABLE products (id TEXT PRIMARY KEY, name TEXT NOT NULL, price REAL NOT NULL);
            """;
        cmd.ExecuteNonQuery();

        using var seedCmd = _connection.CreateCommand();
        seedCmd.CommandText = """
            INSERT INTO users VALUES ('u1','Alice','alice@test.com',30,'US','active');
            INSERT INTO users VALUES ('u2','Bob','bob@test.com',25,'UK','active');
            INSERT INTO users VALUES ('u3','Charlie','charlie@test.com',45,'US','inactive');
            INSERT INTO users VALUES ('u4','Diana','diana@test.com',35,'AU','active');
            INSERT INTO users VALUES ('u5','Eve','eve@test.com',22,'US','active');
            INSERT INTO orders VALUES ('o1','u1',150.00,'completed');
            INSERT INTO orders VALUES ('o2','u1',75.50,'completed');
            INSERT INTO orders VALUES ('o3','u2',200.00,'pending');
            INSERT INTO orders VALUES ('o4','u3',50.00,'completed');
            INSERT INTO orders VALUES ('o5','u4',300.00,'shipped');
            INSERT INTO orders VALUES ('o6','u4',125.00,'completed');
            INSERT INTO orders VALUES ('o7','u5',45.00,'pending');
            INSERT INTO products VALUES ('p1','Widget',9.99);
            INSERT INTO products VALUES ('p2','Gadget',19.99);
            INSERT INTO products VALUES ('p3','Widget',9.99);
            """;
        seedCmd.ExecuteNonQuery();
    }

    [Fact]
    public void ProcessPipeline_SelectFilterOrderLimit_GeneratesCorrectSql()
    {
        var pipeline = WithIdentity("users");
        pipeline.Steps.Add(
            new SelectStep([ColumnInfo.Named("name"), ColumnInfo.Named("age")])
            {
                Base = Id("users"),
            }
        );
        pipeline.Steps.Add(
            new FilterStep
            {
                Base = Id("users"),
                Condition = WhereCondition.Comparison(
                    left: ColumnInfo.Named("age"),
                    @operator: ComparisonOperator.GreaterThan,
                    right: "25"
                ),
            }
        );
        pipeline.Steps.Add(new OrderByStep([("name", "ASC")]) { Base = Id("users") });
        pipeline.Steps.Add(new LimitStep { Base = Id("users"), Count = "10" });

        var sql = new SQLiteContext().ProcessPipeline(pipeline);

        Assert.Contains("SELECT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name", sql);
        Assert.Contains("age", sql);
        Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("> 25", sql);
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT 10", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PipelineProcessor_GroupByWithHaving_GeneratesGroupByAndHavingSql()
    {
        var pipeline = CountByCountryPipeline();
        pipeline.Steps.Add(new HavingStep { Base = Id("users"), Condition = "COUNT(*) > 1" });

        var context = new SQLiteContext();
        var sql = PipelineProcessor.ConvertPipelineToSql(pipeline: pipeline, context: context);

        Assert.Contains("GROUP BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("country", sql);
        Assert.Contains("COUNT(*)", sql);
        Assert.Contains("HAVING", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COUNT(*) > 1", sql);
    }

    [Fact]
    public void PipelineProcessor_OffsetStep_GeneratesOffsetSql()
    {
        var pipeline = WithIdentity("users");
        pipeline.Steps.Add(new SelectStep([ColumnInfo.Named("name")]) { Base = Id("users") });
        pipeline.Steps.Add(new LimitStep { Base = Id("users"), Count = "10" });
        pipeline.Steps.Add(new OffsetStep { Base = Id("users"), Count = "5" });

        var context = new SQLiteContext();
        var sql = PipelineProcessor.ConvertPipelineToSql(pipeline: pipeline, context: context);

        Assert.Contains("OFFSET 5", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT 10", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name", sql);
    }

    [Fact]
    public void PipelineProcessor_SelectDistinctStep_GeneratesDistinctSql()
    {
        var pipeline = WithIdentity("users");
        pipeline.Steps.Add(
            new SelectDistinctStep([ColumnInfo.Named("country")]) { Base = Id("users") }
        );

        var context = new SQLiteContext();
        var sql = PipelineProcessor.ConvertPipelineToSql(pipeline: pipeline, context: context);

        Assert.Contains("SELECT DISTINCT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("country", sql);
    }

    [Fact]
    public void PipelineProcessor_JoinStep_GeneratesJoinSql()
    {
        var pipeline = WithIdentity("users");
        pipeline.Steps.Add(
            new JoinStep
            {
                Base = Id("users"),
                JoinRelationship = new JoinRelationship(
                    LeftTable: "users",
                    RightTable: "orders",
                    Condition: "users.id = orders.user_id",
                    JoinType: "INNER"
                ),
            }
        );
        pipeline.Steps.Add(
            new SelectStep([
                ColumnInfo.Named(name: "name", tableAlias: "users"),
                ColumnInfo.Named(name: "total", tableAlias: "orders"),
            ])
            {
                Base = Id("users"),
            }
        );

        var context = new SQLiteContext();
        var sql = PipelineProcessor.ConvertPipelineToSql(pipeline: pipeline, context: context);

        Assert.Contains("INNER JOIN", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("orders", sql);
        Assert.Contains("users.id = orders.user_id", sql);
        Assert.Contains("users.name", sql);
        Assert.Contains("orders.total", sql);
    }

    [Fact]
    public void PipelineProcessor_ConvertPipelineToSql_WithFilter_GeneratesSql()
    {
        var pipeline = WithIdentity("users");
        pipeline.Steps.Add(
            new SelectStep([ColumnInfo.Named("name"), ColumnInfo.Named("email")])
            {
                Base = Id("users"),
            }
        );
        pipeline.Steps.Add(
            new FilterStep
            {
                Base = Id("users"),
                Condition = WhereCondition.Comparison(
                    left: ColumnInfo.Named("status"),
                    @operator: ComparisonOperator.Eq,
                    right: "'active'"
                ),
            }
        );

        var context = new SQLiteContext();
        var sql = PipelineProcessor.ConvertPipelineToSql(pipeline: pipeline, context: context);

        Assert.Contains("SELECT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name", sql);
        Assert.Contains("email", sql);
        Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("= 'active'", sql);
    }

    [Fact]
    public void PipelineProcessor_EmptyPipeline_ReturnsComment()
    {
        var sql = PipelineProcessor.ConvertPipelineToSql(
            pipeline: new Pipeline(),
            context: new SQLiteContext()
        );
        Assert.Equal("-- Empty pipeline", sql);
    }

    [Fact]
    public void SQLiteContext_AddUnion_DoesNotThrow()
    {
        var context = new SQLiteContext();
        context.SetBaseTable("users");
        context.SetSelectColumns([ColumnInfo.Named("name")]);
        context.AddUnion(query: "SELECT name FROM orders", isUnionAll: false);

        var sql = context.GenerateSQL();

        Assert.Contains("SELECT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name", sql);
        Assert.Contains("users", sql);
    }

    [Fact]
    public void FullPipeline_ExecuteAgainstRealDb_ReturnsCorrectResults()
    {
        var pipeline = WithIdentity("users");
        pipeline.Steps.Add(
            new SelectStep([ColumnInfo.Named("name"), ColumnInfo.Named("age")])
            {
                Base = Id("users"),
            }
        );
        pipeline.Steps.Add(
            new FilterStep
            {
                Base = Id("users"),
                Condition = WhereCondition.Comparison(
                    left: ColumnInfo.Named("age"),
                    @operator: ComparisonOperator.GreaterThan,
                    right: "25"
                ),
            }
        );
        pipeline.Steps.Add(new OrderByStep([("age", "ASC")]) { Base = Id("users") });
        pipeline.Steps.Add(new LimitStep { Base = Id("users"), Count = "3" });

        var context = new SQLiteContext();
        var sql = PipelineProcessor.ConvertPipelineToSql(pipeline: pipeline, context: context);

        var queryResult = _connection.Query<(string, long)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetInt64(1))
        );
        if (
            queryResult
            is not Result<IReadOnlyList<(string, long)>, SqlError>.Ok<
                IReadOnlyList<(string, long)>,
                SqlError
            > ok
        )
        {
            Assert.Fail("Expected Ok result from query execution");
            return;
        }

        var rows = ok.Value;
        Assert.Equal(3, rows.Count);
        Assert.Equal("Alice", rows[0].Item1);
        Assert.Equal(30L, rows[0].Item2);
        Assert.Equal("Diana", rows[1].Item1);
        Assert.Equal("Charlie", rows[2].Item1);
    }

    [Fact]
    public void PipelineProcessor_DistinctExecute_ReturnsDistinctRows()
    {
        var pipeline = WithIdentity("products");
        pipeline.Steps.Add(
            new SelectDistinctStep([ColumnInfo.Named("name")]) { Base = Id("products") }
        );

        var context = new SQLiteContext();
        var sql = PipelineProcessor.ConvertPipelineToSql(pipeline: pipeline, context: context);
        var queryResult = _connection.Query<string>(sql: sql, mapper: r => r.GetString(0));

        if (
            queryResult
            is not Result<IReadOnlyList<string>, SqlError>.Ok<IReadOnlyList<string>, SqlError> ok
        )
        {
            Assert.Fail("Expected Ok result from distinct query");
            return;
        }

        Assert.Equal(2, ok.Value.Count);
        Assert.Contains("Widget", ok.Value);
        Assert.Contains("Gadget", ok.Value);
    }

    [Fact]
    public void PipelineProcessor_GroupByExecute_ReturnsAggregatedRows()
    {
        var pipeline = CountByCountryPipeline();
        pipeline.Steps.Add(new OrderByStep([("cnt", "DESC")]) { Base = Id("users") });

        var context = new SQLiteContext();
        var sql = PipelineProcessor.ConvertPipelineToSql(pipeline: pipeline, context: context);
        var queryResult = _connection.Query<(string, long)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetInt64(1))
        );
        if (
            queryResult
            is not Result<IReadOnlyList<(string, long)>, SqlError>.Ok<
                IReadOnlyList<(string, long)>,
                SqlError
            > ok
        )
        {
            Assert.Fail("Expected Ok result from group by query");
            return;
        }

        Assert.Equal(3, ok.Value.Count);
        Assert.Equal("US", ok.Value[0].Item1);
        Assert.Equal(3L, ok.Value[0].Item2);
    }

    [Fact]
    public void SQLiteFunctionMappingLocal_FunctionsHandlersAndSyntax()
    {
        var mapping = SQLiteFunctionMappingLocal.Instance;

        var countMap = mapping.GetFunctionMapping("count");
        Assert.NotNull(countMap);
        Assert.Equal("COUNT", countMap.SqlFunction);
        Assert.True(countMap.RequiresSpecialHandling);
        Assert.NotNull(countMap.SpecialHandler);
        Assert.Equal("COUNT(*)", countMap.SpecialHandler(["*"]));
        Assert.Equal("COUNT(id)", countMap.SpecialHandler(["id"]));

        Assert.Equal("SUM", mapping.GetFunctionMapping("sum")?.SqlFunction);
        Assert.Equal("UPPER", mapping.GetFunctionMapping("upper")?.SqlFunction);
        Assert.Equal("LOWER", mapping.GetFunctionMapping("lower")?.SqlFunction);

        var substringMap = mapping.GetFunctionMapping("substring");
        Assert.NotNull(substringMap);
        Assert.NotNull(substringMap.SpecialHandler);
        Assert.Equal("substr(name, 1, 3)", substringMap.SpecialHandler(["name", "1", "3"]));

        var dateMap = mapping.GetFunctionMapping("current_date");
        Assert.NotNull(dateMap);
        Assert.NotNull(dateMap.SpecialHandler);
        Assert.Equal("datetime('now')", dateMap.SpecialHandler([]));

        Assert.Null(mapping.GetFunctionMapping("nonexistent_function"));

        var syntax = mapping.GetSyntaxMapping();
        Assert.Equal("LIMIT {0}", syntax.LimitClause);
        Assert.Equal("OFFSET {0}", syntax.OffsetClause);
        Assert.Equal("datetime('now')", syntax.DateCurrentFunction);
        Assert.Equal("LENGTH", syntax.StringLengthFunction);
        Assert.Equal("||", syntax.StringConcatOperator);
        Assert.Equal("\"", syntax.IdentifierQuoteChar);
        Assert.False(syntax.SupportsBoolean);
    }

    [Fact]
    public void SQLiteContext_ToSQLiteSql_GeneratesFromStatement()
    {
        var statement = new SelectStatementBuilder()
            .AddTable("users")
            .AddSelectColumn("name")
            .AddSelectColumn("email")
            .WithLimit("5")
            .Build();
        var sql = SQLiteContext.ToSQLiteSql(statement);

        Assert.Contains("SELECT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name", sql);
        Assert.Contains("email", sql);
        Assert.Contains("FROM users", sql);
        Assert.Contains("LIMIT 5", sql);
    }
}
