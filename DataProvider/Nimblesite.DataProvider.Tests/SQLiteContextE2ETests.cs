// Implements [CI-TEST-SHARED-COMMAND].

using Nimblesite.Lql.Core;
using Nimblesite.Lql.SQLite;
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

    private static string ConvertPipeline(Pipeline pipeline)
    {
        var context = new SQLiteContext();
        return PipelineProcessor.ConvertPipelineToSql(pipeline: pipeline, context: context);
    }

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

    private void CreateSchemaAndSeed() =>
        SqliteSchemaSetup.Apply(
            connection: _connection,
            command => command.CommandText = Data_CreateSchemaAndSeed_1,
            command => command.CommandText = Data_CreateSchemaAndSeed_2
        );

    [Fact]
    public void ProcessPipeline_SelectFilterOrderLimit_GeneratesCorrectSql()
    {
        var pipeline = BuildFilteredUserPipeline(value7: "name", value11: "10");

        var sql = new SQLiteContext().ProcessPipeline(pipeline);

        AssertGeneratedSql(
            sql: sql,
            selectFragment: "SELECT",
            firstColumn: "name",
            secondColumn: "age",
            whereFragment: "WHERE",
            whereValue: "> 25"
        );
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

        VerifyCompletionWorkflow1(
            input0: ConvertPipeline,
            input1: pipeline,
            input2: AssertGeneratedSql,
            value0: "GROUP BY",
            value1: "country",
            value2: "COUNT(*)",
            value3: "HAVING",
            value4: "COUNT(*) > 1"
        );
    }

    [Fact]
    public void PipelineProcessor_OffsetStep_GeneratesOffsetSql()
    {
        var pipeline = WithIdentity("users");
        pipeline.Steps.Add(new SelectStep([ColumnInfo.Named("name")]) { Base = Id("users") });
        pipeline.Steps.Add(new LimitStep { Base = Id("users"), Count = "10" });
        pipeline.Steps.Add(new OffsetStep { Base = Id("users"), Count = "5" });

        var sql = ConvertPipeline(pipeline);

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

        var sql = ConvertPipeline(pipeline);

        global::Nimblesite.DataProvider.Tests.QueryBuilderE2ETests.VerifyReusableWorkflow1(
            input0: sql,
            value0: "SELECT DISTINCT",
            value2: "country"
        );
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

        var sql = ConvertPipeline(pipeline);

        Assert.Contains("INNER JOIN", sql, StringComparison.OrdinalIgnoreCase);
        ContainsAll(
            actual: sql,
            expected: ["orders", "users.id = orders.user_id", "users.name", "orders.total"]
        );
    }

    [Fact]
    public void PipelineProcessor_ConvertPipelineToSql_WithFilter_GeneratesSql()
    {
        var pipeline = BuildSelectFilterPipeline(
            columns: [ColumnInfo.Named("name"), ColumnInfo.Named("email")],
            filterColumn: "status",
            @operator: ComparisonOperator.Eq,
            right: "'active'"
        );

        VerifyCompletionWorkflow1(
            input0: ConvertPipeline,
            input1: pipeline,
            input2: AssertGeneratedSql,
            value0: "SELECT",
            value1: "name",
            value2: "email",
            value3: "WHERE",
            value4: "= 'active'"
        );
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

        global::Nimblesite.DataProvider.Tests.LqlSqliteE2ETests.VerifyReusableWorkflow5(
            input0: sql,
            input1: ContainsAll,
            value0: "SELECT",
            value2: "name",
            value3: "users"
        );
    }

    [Fact]
    public void FullPipeline_ExecuteAgainstRealDb_ReturnsCorrectResults()
    {
        var pipeline = BuildFilteredUserPipeline(value7: "age", value11: "3");

        var ok = VerifySharedChecks4(
            input0: pipeline,
            input1: _connection,
            value2: "Expected Ok result from query execution"
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

        var sql = ConvertPipeline(pipeline);
        var queryResult = _connection.Query<string>(sql: sql, mapper: r => r.GetString(0));

        global::Nimblesite.DataProvider.Tests.SelectStatementLinqE2ETests.VerifyReusableWorkflow13(
            input0: queryResult,
            value0: "Expected Ok result from distinct query",
            value1: 2,
            value2: "Widget",
            value3: "Gadget"
        );
    }

    [Fact]
    public void PipelineProcessor_GroupByExecute_ReturnsAggregatedRows()
    {
        var pipeline = CountByCountryPipeline();
        pipeline.Steps.Add(new OrderByStep([("cnt", "DESC")]) { Base = Id("users") });

        var ok = VerifySharedChecks4(
            input0: pipeline,
            input1: _connection,
            value2: "Expected Ok result from group by query"
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

    // Implements [CI-TEST-SHARED-SCENARIOS]: shared two-stage SQL fragment checks
    // used by every pipeline generation test.
    private static void AssertGeneratedSql(
        string sql,
        string selectFragment,
        string firstColumn,
        string secondColumn,
        string whereFragment,
        string whereValue
    )
    {
        global::Nimblesite.DataProvider.Tests.LqlSqliteE2ETests.VerifyReusableWorkflow5(
            input0: sql,
            input1: ContainsAll,
            value0: selectFragment,
            value2: firstColumn,
            value3: secondColumn
        );
        global::Nimblesite.DataProvider.Tests.QueryBuilderE2ETests.VerifyReusableWorkflow1(
            input0: sql,
            value0: whereFragment,
            value2: whereValue
        );
    }

    // Implements [CI-TEST-SHARED-SCENARIOS]: shared select+filter pipeline skeleton.
    private static Pipeline BuildSelectFilterPipeline(
        IEnumerable<ColumnInfo> columns,
        string filterColumn,
        ComparisonOperator @operator,
        string right
    )
    {
        var pipeline = WithIdentity("users");
        pipeline.Steps.Add(new SelectStep(columns) { Base = Id("users") });
        pipeline.Steps.Add(
            new FilterStep
            {
                Base = Id("users"),
                Condition = WhereCondition.Comparison(
                    left: ColumnInfo.Named(filterColumn),
                    @operator: @operator,
                    right: right
                ),
            }
        );
        return pipeline;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static global::Nimblesite.Lql.Core.Pipeline BuildFilteredUserPipeline(
        string value7,
        string value11
    )
    {
        var pipeline = BuildSelectFilterPipeline(
            columns: [ColumnInfo.Named("name"), ColumnInfo.Named("age")],
            filterColumn: "age",
            @operator: ComparisonOperator.GreaterThan,
            right: "25"
        );
        pipeline.Steps.Add(new OrderByStep([(value7, "ASC")]) { Base = Id("users") });
        pipeline.Steps.Add(new LimitStep { Base = Id("users"), Count = value11 });

        return pipeline;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static Result<IReadOnlyList<(string, long)>, global::Nimblesite.Sql.Model.SqlError>.Ok<
        IReadOnlyList<(string, long)>,
        global::Nimblesite.Sql.Model.SqlError
    > VerifySharedChecks4(Pipeline input0, SqliteConnection input1, string value2)
    {
        var sql = ConvertPipeline(input0);

        var queryResult = input1.Query<(string, long)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetInt64(1))
        );
        var ok = global::Tests.Shared.ResultAssertions.Expected<Result<
            IReadOnlyList<(string, long)>,
            SqlError
        >.Ok<IReadOnlyList<(string, long)>, SqlError>>(actual: queryResult, message: value2);

        return ok;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyCompletionWorkflow1(
        Func<Pipeline, string> input0,
        Pipeline input1,
        Action<string, string, string, string, string, string> input2,
        string value0,
        string value1,
        string value2,
        string value3,
        string value4
    )
    {
        var sql = input0(arg: input1);

        input2(arg1: sql, arg2: value0, arg3: value1, arg4: value2, arg5: value3, arg6: value4);
    }
}
