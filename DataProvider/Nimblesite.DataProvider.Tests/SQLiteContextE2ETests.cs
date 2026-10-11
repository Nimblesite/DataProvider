// Implements [CI-TEST-SHARED-COMMAND].
using Microsoft.Data.Sqlite;
using Nimblesite.Lql.Core;
using Nimblesite.Lql.SQLite;
using Nimblesite.Sql.Model;
using Outcome;
using Tests.Shared;
using static global::Tests.Shared.StringAssertions;

namespace Nimblesite.DataProvider.Tests;

public sealed partial class SQLiteContextE2ETests : IDisposable
{
    private readonly string _dbPath = global::Tests.Shared.TempSqliteDatabase.PathFor("ctx_e2e");
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
        DatabaseCommands.Execute(
            connection: _connection,
            configure: command => command.CommandText = Data_CreateSchemaAndSeed_1
        );

        DatabaseCommands.Execute(
            connection: _connection,
            configure: command => command.CommandText = Data_CreateSchemaAndSeed_2
        );
    }

    [Fact]
    public void ProcessPipeline_SelectFilterOrderLimit_GeneratesCorrectSql()
    {
        var pipeline = BuildFilteredUserPipeline(value7: "name", value11: "10");

        var sql = new SQLiteContext().ProcessPipeline(pipeline);

        Assert.Contains("SELECT", sql, StringComparison.OrdinalIgnoreCase);
        ContainsAll(actual: sql, expected: ["name", "age"]);
        Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("> 25", sql);
        ContainsAll(
            actual: sql,
            expected: ["ORDER BY", "LIMIT 10"],
            comparison: StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void PipelineProcessor_GroupByWithHaving_GeneratesGroupByAndHavingSql()
    {
        var pipeline = CountByCountryPipeline();
        pipeline.Steps.Add(new HavingStep { Base = Id("users"), Condition = "COUNT(*) > 1" });

        var context = new SQLiteContext();
        var sql = PipelineProcessor.ConvertPipelineToSql(pipeline: pipeline, context: context);

        Assert.Contains("GROUP BY", sql, StringComparison.OrdinalIgnoreCase);
        ContainsAll(actual: sql, expected: ["country", "COUNT(*)"]);
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

        ContainsAll(
            actual: sql,
            expected: ["OFFSET 5", "LIMIT 10"],
            comparison: StringComparison.OrdinalIgnoreCase
        );
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
                JoinRelationship = Dataset_PipelineProcessor_JoinStep_GeneratesJoinSql_1(),
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
        ContainsAll(
            actual: sql,
            expected: ["orders", "users.id = orders.user_id", "users.name", "orders.total"]
        );
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
        ContainsAll(actual: sql, expected: ["name", "email"]);
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
        ContainsAll(actual: sql, expected: ["name", "users"]);
    }

    [Fact]
    public void FullPipeline_ExecuteAgainstRealDb_ReturnsCorrectResults()
    {
        var pipeline = BuildFilteredUserPipeline(value7: "age", value11: "3");

        var context = new SQLiteContext();
        var sql = PipelineProcessor.ConvertPipelineToSql(pipeline: pipeline, context: context);

        var queryResult = _connection.Query<(string, long)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetInt64(1))
        );
        var ok = global::Tests.Shared.ResultAssertions.Expected<Result<
            IReadOnlyList<(string, long)>,
            SqlError
        >.Ok<IReadOnlyList<(string, long)>, SqlError>>(
            actual: queryResult,
            message: "Expected Ok result from query execution"
        );

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

        var ok = global::Tests.Shared.ResultAssertions.Expected<Result<
            IReadOnlyList<string>,
            SqlError
        >.Ok<IReadOnlyList<string>, SqlError>>(
            actual: queryResult,
            message: "Expected Ok result from distinct query"
        );

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
        var ok = global::Tests.Shared.ResultAssertions.Expected<Result<
            IReadOnlyList<(string, long)>,
            SqlError
        >.Ok<IReadOnlyList<(string, long)>, SqlError>>(
            actual: queryResult,
            message: "Expected Ok result from group by query"
        );

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
        ContainsAll(actual: sql, expected: ["name", "email", "FROM users", "LIMIT 5"]);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static global::Nimblesite.Lql.Core.Pipeline BuildFilteredUserPipeline(
        string value7,
        string value11
    )
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
        pipeline.Steps.Add(new OrderByStep([(value7, "ASC")]) { Base = Id("users") });
        pipeline.Steps.Add(new LimitStep { Base = Id("users"), Count = value11 });

        return pipeline;
    }
}
