using static global::Tests.Shared.ResultAssertions;

namespace Nimblesite.DataProvider.Tests;

/// <summary>
/// Tests for DbConnectionExtensions methods to improve coverage
/// </summary>
public sealed partial class DbConnectionExtensionsTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _dbPath;

    public DbConnectionExtensionsTests()
    {
        (_dbPath, _connection) = global::Tests.Shared.TempSqliteDatabase.Create("dbconn_ext_tests");
        CreateSchema();
    }

    //TODO: this is illegal because this should be using migrations
    private void CreateSchema()
    {
        using var command = new SqliteCommand(Data_CreateSchema_1, _connection);
        command.ExecuteNonQuery();
    }

    [Fact]
    public void Query_WithNullConnection_ReturnsError()
    {
        SqliteConnection? nullConnection = null;

        var error = (
            Error(read: () =>
                nullConnection!.Query(
                    "SELECT * FROM TestTable",
                    null,
                    reader => new TestRecord
                    {
                        Name = reader.GetString(1),
                        Value = reader.GetInt32(2),
                    }
                )
            )
        ).Value;
        Assert.Contains("null", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Query_WithNullSql_ReturnsError()
    {
        var error = (
            Error(read: () =>
                _connection.Query(
                    null!,
                    null,
                    reader => new TestRecord
                    {
                        Name = reader.GetString(1),
                        Value = reader.GetInt32(2),
                    }
                )
            )
        ).Value;
        Assert.Contains("null", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(Query_WithEmptySql_ReturnsError_CasesData))]
    public void Query_WithEmptySql_ReturnsError_Cases(string caseName, string value0) =>
        ExtensionTestCases.RunCase(
            caseName: caseName,
            caseBody: () =>
                ExtensionTestCases.ExpectQueryError<TestRecord>(
                    sql: value0,
                    query: sql =>
                        _connection.Query(
                            sql,
                            null,
                            reader => new TestRecord
                            {
                                Name = reader.GetString(1),
                                Value = reader.GetInt32(2),
                            }
                        )
                )
        );

    [Fact]
    public void Query_WithValidSql_ReturnsResults()
    {
        var result = _connection.Query(
            "SELECT Id, Name, Value FROM TestTable ORDER BY Name",
            null,
            reader => new TestRecord { Name = reader.GetString(1), Value = reader.GetInt32(2) }
        );

        var records = RequireQueryRecords(input0: result);
        Assert.Equal(3, records.Count);
        Assert.Equal("Alpha", records[0].Name);
    }

    [Fact]
    public void Query_WithParameters_ReturnsFilteredResults()
    {
        var result = _connection.Query(
            "SELECT Id, Name, Value FROM TestTable WHERE Value > @minValue ORDER BY Value",
            [new SqliteParameter("@minValue", 15)],
            reader => new TestRecord { Name = reader.GetString(1), Value = reader.GetInt32(2) }
        );

        var records = RequireQueryRecords(input0: result);
        Assert.Equal(2, records.Count);
        Assert.Equal("Beta", records[0].Name);
        Assert.Equal("Gamma", records[1].Name);
    }

    [Fact]
    public void Query_WithNullMapper_ReturnsEmptyResults()
    {
        var result = _connection.Query<TestRecord>(
            "SELECT Id, Name, Value FROM TestTable",
            null,
            null
        );

        var records = RequireQueryRecords(input0: result);
        Assert.Empty(records);
    }

    [Fact]
    public void Execute_WithNullConnection_ReturnsError()
    {
        SqliteConnection? nullConnection = null;

        var result = nullConnection!.Execute(
            "INSERT INTO TestTable (Name, Value) VALUES ('Test', 100)"
        );

        var error = Expected<IntError>(actual: result).Value;
        Assert.Contains("null", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_WithNullSql_ReturnsError()
    {
        var result = _connection.Execute(null!);

        var error = Expected<IntError>(actual: result).Value;
        Assert.Contains("null", error.Message, StringComparison.OrdinalIgnoreCase);
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
                    execute: sql => _connection.Execute(sql)
                )
        );

    [Fact]
    public void Execute_WithValidSql_ReturnsRowsAffected()
    {
        var rowsAffected = Ok(read: () =>
            _connection.Execute(
                "INSERT INTO TestTable (Name, Value) VALUES (@name, @value)",
                [new SqliteParameter("@name", "Delta"), new SqliteParameter("@value", 40)]
            )
        ).Value;
        Assert.Equal(1, rowsAffected);
    }

    [Fact]
    public void Scalar_WithNullConnection_ReturnsError()
    {
        SqliteConnection? nullConnection = null;

        var result = nullConnection!.Scalar<string>("SELECT Name FROM TestTable LIMIT 1");

        Assert.False(result is NullableStringOk);
    }

    [Fact]
    public void Scalar_WithNullSql_ReturnsError()
    {
        var result = _connection.Scalar<string>(null!);

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
                    scalar: sql => _connection.Scalar<string>(sql)
                )
        );

    [Fact]
    public void Scalar_WithValidSql_ReturnsValue()
    {
        var result = _connection.Scalar<string>("SELECT Name FROM TestTable ORDER BY Name LIMIT 1");

        global::Nimblesite.DataProvider.Tests.DbTransactionExtensionsTests.VerifyReusableWorkflow3(
            input0: result,
            value0: "Alpha"
        );
    }

    [Fact]
    public void Scalar_WithParameters_ReturnsFilteredValue()
    {
        var result = _connection.Scalar<string>(
            "SELECT Name FROM TestTable WHERE Value > @minValue ORDER BY Value LIMIT 1",
            [new SqliteParameter("@minValue", 15)]
        );

        global::Nimblesite.DataProvider.Tests.DbTransactionExtensionsTests.VerifyReusableWorkflow3(
            input0: result,
            value0: "Beta"
        );
    }

    public void Dispose()
    {
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

    private sealed record TestRecord
    {
        public string Name { get; init; } = "";
        public int Value { get; init; }
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Tests.DbConnectionExtensionsTests.TestRecord> RequireQueryRecords(
        global::Outcome.Result<
            global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Tests.DbConnectionExtensionsTests.TestRecord>,
            global::Nimblesite.Sql.Model.SqlError
        > input0
    )
    {
        var records = Ok(actual: input0).Value;

        return records;
    }
}
