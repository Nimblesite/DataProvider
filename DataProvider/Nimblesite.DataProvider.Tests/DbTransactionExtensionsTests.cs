using Microsoft.Data.Sqlite;
using TestRecordListError = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.DataProvider.Tests.DbTransactionExtensionsTests.TestRecord>,
    Nimblesite.Sql.Model.SqlError
>.Error<
    System.Collections.Generic.IReadOnlyList<Nimblesite.DataProvider.Tests.DbTransactionExtensionsTests.TestRecord>,
    Nimblesite.Sql.Model.SqlError
>;
using TestRecordListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.DataProvider.Tests.DbTransactionExtensionsTests.TestRecord>,
    Nimblesite.Sql.Model.SqlError
>.Ok<
    System.Collections.Generic.IReadOnlyList<Nimblesite.DataProvider.Tests.DbTransactionExtensionsTests.TestRecord>,
    Nimblesite.Sql.Model.SqlError
>;

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
        _dbPath = Path.Combine(Path.GetTempPath(), $"dbtrans_ext_tests_{Guid.NewGuid()}.db");
        _connection = new SqliteConnection($"Data Source={_dbPath}");
        _connection.Open();
        CreateSchema();
        _transaction = _connection.BeginTransaction();
    }

    private void CreateSchema()
    {
        using var command = new SqliteCommand(
            Data_CreateSchema_1,
            _connection
        );
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

        var result = _transaction.Query(
            "SELECT Name, Value FROM TestTable ORDER BY Name",
            [],
            reader => new TestRecord(reader.GetString(0), reader.GetInt32(1))
        );

        Assert.True(result is TestRecordListOk);
        if (result is TestRecordListOk ok)
        {
            Assert.Equal(2, ok.Value.Count);
            Assert.Equal("Test1", ok.Value[0].Name);
            Assert.Equal(100, ok.Value[0].Value);
            Assert.Equal("Test2", ok.Value[1].Name);
            Assert.Equal(200, ok.Value[1].Value);
        }
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

        Assert.True(result is TestRecordListOk);
        if (result is TestRecordListOk ok)
        {
            Assert.Equal(2, ok.Value.Count);
            Assert.Equal("Beta", ok.Value[0].Name);
            Assert.Equal(20, ok.Value[0].Value);
            Assert.Equal("Gamma", ok.Value[1].Name);
            Assert.Equal(30, ok.Value[1].Value);
        }
    }

    [Fact]
    public void Query_WithEmptyResult_ReturnsEmptyList()
    {
        var result = _transaction.Query(
            "SELECT Name, Value FROM TestTable WHERE 1=0",
            [],
            reader => new TestRecord(reader.GetString(0), reader.GetInt32(1))
        );

        Assert.True(result is TestRecordListOk);
        if (result is TestRecordListOk ok)
        {
            Assert.Empty(ok.Value);
        }
    }

    [Fact]
    public void Query_WithSqlError_ReturnsFailure()
    {
        var result = _transaction.Query(
            "SELECT InvalidColumn FROM NonExistentTable",
            [],
            reader => new TestRecord(reader.GetString(0), reader.GetInt32(1))
        );

        Assert.True(result is TestRecordListError);
        if (result is TestRecordListError err)
        {
            Assert.NotNull(err.Value.Message);
            Assert.Contains("no such table", err.Value.Message);
        }
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

        Assert.True(result is TestRecordListError);
        if (result is TestRecordListError err)
        {
            Assert.Contains("null", err.Value.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Query_WithNullSql_ReturnsError()
    {
        var result = _transaction.Query(
            null!,
            null,
            reader => new TestRecord(reader.GetString(0), reader.GetInt32(1))
        );

        Assert.True(result is TestRecordListError);
        if (result is TestRecordListError err)
        {
            Assert.Contains("null", err.Value.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Query_WithEmptySql_ReturnsError()
    {
        var result = _transaction.Query(
            "   ",
            null,
            reader => new TestRecord(reader.GetString(0), reader.GetInt32(1))
        );

        Assert.True(result is TestRecordListError);
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

        Assert.True(result is TestRecordListOk);
        if (result is TestRecordListOk ok)
        {
            Assert.Empty(ok.Value);
        }
    }

    [Fact]
    public void Execute_WithNullTransaction_ReturnsError()
    {
        SqliteTransaction? nullTransaction = null;

        var result = nullTransaction!.Execute(
            "INSERT INTO TestTable (Name, Value) VALUES ('Test', 100)"
        );

        Assert.True(result is IntError);
        if (result is IntError err)
        {
            Assert.Contains("null", err.Value.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Execute_WithNullSql_ReturnsError()
    {
        var result = _transaction.Execute(null!);

        Assert.True(result is IntError);
        if (result is IntError err)
        {
            Assert.Contains("null", err.Value.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Execute_WithEmptySql_ReturnsError()
    {
        var result = _transaction.Execute("   ");

        Assert.True(result is IntError);
    }

    [Fact]
    public void Execute_WithValidSql_ReturnsRowsAffected()
    {
        var result = _transaction.Execute(
            "INSERT INTO TestTable (Name, Value) VALUES (@name, @value)",
            [new SqliteParameter("@name", "Delta"), new SqliteParameter("@value", 40)]
        );

        Assert.True(result is IntOk);
        if (result is IntOk ok)
        {
            Assert.Equal(1, ok.Value);
        }
    }

    [Fact]
    public void Execute_WithInvalidSql_ReturnsError()
    {
        var result = _transaction.Execute("INSERT INTO NonExistentTable (Name) VALUES ('Test')");

        Assert.True(result is IntError);
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

    [Fact]
    public void Scalar_WithEmptySql_ReturnsError()
    {
        var result = _transaction.Scalar<string>("   ");

        Assert.False(result is NullableStringOk);
    }

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

        Assert.True(result is NullableStringOk);
        if (result is NullableStringOk ok)
        {
            Assert.Equal("Alpha", ok.Value);
        }
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

        Assert.True(result is NullableStringOk);
        if (result is NullableStringOk ok)
        {
            Assert.Equal("Beta", ok.Value);
        }
    }

    [Fact]
    public void Scalar_WithInvalidSql_ReturnsError()
    {
        var result = _transaction.Scalar<string>("SELECT Name FROM NonExistentTable");

        Assert.False(result is NullableStringOk);
    }

    /// <summary>
    /// Test record for query results.
    /// </summary>
    /// <param name="Name">The name.</param>
    /// <param name="Value">The value.</param>
    internal sealed record TestRecord(string Name, int Value);
}
