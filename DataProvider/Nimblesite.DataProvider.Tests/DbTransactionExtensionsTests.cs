namespace Nimblesite.DataProvider.Tests;

/// <summary>
/// Tests for DbTransactionExtensions Query method to improve coverage
/// </summary>
public sealed partial class DbTransactionExtensionsTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction _transaction;
    private readonly string _dbPath;

    /// <summary>
    /// Initializes a new instance of <see cref="DbTransactionExtensionsTests"/>.
    /// </summary>
    public DbTransactionExtensionsTests()
    {
        (_dbPath, _connection) = global::Tests.Shared.TempSqliteDatabase.Create(
            "dbtrans_ext_tests"
        );
        CreateSchema();
        _transaction = _connection.BeginTransaction();
    }

    private void CreateSchema()
    {
        using var command = new SqliteCommand(Data_CreateSchema_1, _connection);
        command.ExecuteNonQuery();
    }

    [Fact]
    public void Query_WithValidData_ReturnsResults()
    {
        _transaction.Execute(
            "INSERT INTO TestTable (Name, Value) VALUES (@name1, @value1), (@name2, @value2)",
            [
                new SqliteParameter("@name1", "Test1"),
                new SqliteParameter("@value1", 100),
                new SqliteParameter("@name2", "Test2"),
                new SqliteParameter("@value2", 200),
            ]
        );

        var result = QueryTestRecords(sql: "SELECT Name, Value FROM TestTable ORDER BY Name");

        VerifyReusableWorkflow18(
            input0: result,
            value1: "Test1",
            value3: 100,
            value5: "Test2",
            value7: 200
        );
    }

    [Fact]
    public void Query_WithParameters_ReturnsFilteredResults()
    {
        _transaction.Execute(
            "INSERT INTO TestTable (Name, Value) VALUES (@name1, @value1), (@name2, @value2), (@name3, @value3)",
            [
                new SqliteParameter("@name1", "Alpha"),
                new SqliteParameter("@value1", 10),
                new SqliteParameter("@name2", "Beta"),
                new SqliteParameter("@value2", 20),
                new SqliteParameter("@name3", "Gamma"),
                new SqliteParameter("@value3", 30),
            ]
        );

        var result = _transaction.Query(
            "SELECT Name, Value FROM TestTable WHERE Value > @minValue ORDER BY Value",
            [new SqliteParameter("@minValue", 15)],
            reader => new TestRecord(reader.GetString(0), reader.GetInt32(1))
        );

        VerifyReusableWorkflow18(
            input0: result,
            value1: "Beta",
            value3: 20,
            value5: "Gamma",
            value7: 30
        );
    }

    [Fact]
    public void Query_WithEmptyResult_ReturnsEmptyList()
    {
        var result = QueryTestRecords(sql: "SELECT Name, Value FROM TestTable WHERE 1=0");

        ExtensionTestCases.ExpectOkEmpty<TestRecord, Sql.Model.SqlError>(actual: result);
    }

    [Fact]
    public void Query_WithSqlError_ReturnsFailure()
    {
        var result = QueryTestRecords(sql: "SELECT InvalidColumn FROM NonExistentTable");

        var err = global::Tests.Shared.ResultAssertions.Error(actual: result).Value;
        Assert.NotNull(err.Message);
        Assert.Contains("no such table", err.Message);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _transaction?.Dispose();
        _connection?.Dispose();
        if (File.Exists(_dbPath))
        {
            try
            {
                File.Delete(_dbPath);
            }
#pragma warning disable CA1031 // Cleanup is best-effort
            catch (IOException)
            {
                // File may be locked by another process
            }
            catch (UnauthorizedAccessException)
            {
                // May not have permission
            }
#pragma warning restore CA1031
        }
    }

    [Fact]
    public void Query_WithNullTransaction_ReturnsError()
    {
        SqliteTransaction? nullTransaction = null;

        var result = nullTransaction!.Query(
            "SELECT * FROM TestTable",
            null,
            reader => new TestRecord(reader.GetString(0), reader.GetInt32(1))
        );

        var err = global::Tests.Shared.ResultAssertions.Error(actual: result).Value;
        Assert.Contains("null", err.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Query_WithNullSql_ReturnsError()
    {
        var result = _transaction.Query(
            null!,
            null,
            reader => new TestRecord(reader.GetString(0), reader.GetInt32(1))
        );

        var err = global::Tests.Shared.ResultAssertions.Error(actual: result).Value;
        Assert.Contains("null", err.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Query_WithEmptySql_ReturnsError()
    {
        global::Tests.Shared.ResultAssertions.Error(
            actual: _transaction.Query(
                "   ",
                null,
                reader => new TestRecord(reader.GetString(0), reader.GetInt32(1))
            )
        );
    }

    [Fact]
    public void Query_WithNullMapper_ReturnsEmptyResults()
    {
        _transaction.Execute(
            "INSERT INTO TestTable (Name, Value) VALUES (@name, @value)",
            [new SqliteParameter("@name", "Test"), new SqliteParameter("@value", 100)]
        );

        var result = _transaction.Query<TestRecord>(
            "SELECT Name, Value FROM TestTable",
            null,
            null
        );

        ExtensionTestCases.ExpectOkEmpty<TestRecord, Sql.Model.SqlError>(actual: result);
    }

    [Fact]
    public void Execute_WithNullTransaction_ReturnsError()
    {
        SqliteTransaction? nullTransaction = null;

        var result = nullTransaction!.Execute(
            "INSERT INTO TestTable (Name, Value) VALUES ('Test', 100)"
        );

        var err = global::Tests.Shared.ResultAssertions.Error(actual: result).Value;
        Assert.Contains("null", err.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_WithNullSql_ReturnsError()
    {
        var result = _transaction.Execute(null!);

        var err = global::Tests.Shared.ResultAssertions.Error(actual: result).Value;
        Assert.Contains("null", err.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(Execute_WithEmptySql_ReturnsError_ContextCasesData))]
    public void Execute_WithEmptySql_ReturnsError_ContextCases(string caseName, string value0) =>
        ExtensionTestCases.RunCase(
            caseName: caseName,
            caseBody: () =>
                ExtensionTestCases.ExpectExecuteError(
                    sql: value0,
                    execute: sql => _transaction.Execute(sql)
                )
        );

    [Fact]
    public void Execute_WithValidSql_ReturnsRowsAffected()
    {
        var result = _transaction.Execute(
            "INSERT INTO TestTable (Name, Value) VALUES (@name, @value)",
            [new SqliteParameter("@name", "Delta"), new SqliteParameter("@value", 40)]
        );

        VerifyReusableWorkflow16(input0: result, value0: 1);
    }

    [Fact]
    public void Scalar_WithNullTransaction_ReturnsError()
    {
        SqliteTransaction? nullTransaction = null;

        var result = nullTransaction!.Scalar<string>("SELECT Name FROM TestTable LIMIT 1");

        Assert.False(result is NullableStringOk);
    }

    [Fact]
    public void Scalar_WithNullSql_ReturnsError()
    {
        var result = _transaction.Scalar<string>(null!);

        Assert.False(result is NullableStringOk);
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(Scalar_WithEmptySql_ReturnsError_ContextCasesData))]
    public void Scalar_WithEmptySql_ReturnsError_ContextCases(string caseName, string value0) =>
        ExtensionTestCases.RunCase(
            caseName: caseName,
            caseBody: () =>
                ExtensionTestCases.ExpectScalarNotOk<string?>(
                    sql: value0,
                    scalar: sql => _transaction.Scalar<string>(sql)
                )
        );

    [Fact]
    public void Scalar_WithValidSql_ReturnsValue()
    {
        _transaction.Execute(
            "INSERT INTO TestTable (Name, Value) VALUES ('Alpha', 1), ('Beta', 2), ('Gamma', 3)",
            []
        );

        var result = _transaction.Scalar<string>(
            "SELECT Name FROM TestTable ORDER BY Name LIMIT 1"
        );

        VerifyReusableWorkflow3(input0: result, value0: "Alpha");
    }

    [Fact]
    public void Scalar_WithParameters_ReturnsFilteredValue()
    {
        _transaction.Execute(
            "INSERT INTO TestTable (Name, Value) VALUES ('Alpha', 10), ('Beta', 20), ('Gamma', 30)",
            []
        );

        var result = _transaction.Scalar<string>(
            "SELECT Name FROM TestTable WHERE Value > @minValue ORDER BY Value LIMIT 1",
            [new SqliteParameter("@minValue", 15)]
        );

        VerifyReusableWorkflow3(input0: result, value0: "Beta");
    }

    /// <summary>
    /// Test record for query results.
    /// </summary>
    /// <param name="Name">The name.</param>
    /// <param name="Value">The value.</param>
    internal sealed record TestRecord(string Name, int Value);

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void VerifyScalarOk<T>(Result<T, Sql.Model.SqlError> input0, T value0) =>
        global::Tests.Shared.ResultAssertions.VerifyOk(
            actual: input0,
            verify: ok => Assert.Equal(value0, ok.Value)
        );

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private Result<IReadOnlyList<TestRecord>, Sql.Model.SqlError> QueryTestRecords(string sql) =>
        _transaction.Query(
            sql,
            [],
            reader => new TestRecord(reader.GetString(0), reader.GetInt32(1))
        );

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyReusableWorkflow3(
        Result<string?, Sql.Model.SqlError> input0,
        string value0
    ) => VerifyScalarOk(input0: input0, value0: value0);

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyReusableWorkflow16(
        Result<int, Sql.Model.SqlError> input0,
        int value0
    ) => VerifyScalarOk(input0: input0, value0: value0);

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyReusableWorkflow18(
        Result<IReadOnlyList<TestRecord>, Sql.Model.SqlError> input0,
        string value1,
        int value3,
        string value5,
        int value7
    )
    {
        global::Tests.Shared.ResultAssertions.VerifyOk(
            actual: input0,
            verify: ok =>
            {
                Assert.Equal(2, ok.Value.Count);
                Assert.Equal(value1, ok.Value[0].Name);
                Assert.Equal(value3, ok.Value[0].Value);
                Assert.Equal(value5, ok.Value[1].Name);
                Assert.Equal(value7, ok.Value[1].Value);
            }
        );
    }
}
