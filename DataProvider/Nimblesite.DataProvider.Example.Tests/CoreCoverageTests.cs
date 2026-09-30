using System.Data;
using Microsoft.Data.Sqlite;
using Nimblesite.Lql.SQLite;
using Nimblesite.Sql.Model;
using Outcome;
using Xunit;

namespace Nimblesite.DataProvider.Example.Tests;

#pragma warning disable CS1591

/// <summary>
/// Tests for DataProvider.Core methods: DbConnectionExtensions, DbTransact, DbTransactionExtensions
/// </summary>
public sealed class CoreCoverageTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnection _connection;

    public CoreCoverageTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"core_coverage_tests_{Guid.NewGuid()}.db");
        _connection = new SqliteConnection($"Data Source={_dbPath}");
    }

    #region DbConnectionExtensions.Query

    [Fact]
    public async Task Query_WithValidSqlAndMapper_ReturnsResults()
    {
        await SetupDatabase().ConfigureAwait(false);

        var result = _connection.Query(
            sql: "SELECT Id, CustomerName, Email FROM Customer",
            mapper: reader =>
                (Id: reader.GetString(0), Name: reader.GetString(1), Email: reader.GetString(2))
        );

        Assert.True(
            result
                is Result<IReadOnlyList<(string Id, string Name, string Email)>, SqlError>.Ok<
                    IReadOnlyList<(string Id, string Name, string Email)>,
                    SqlError
                >
        );
        var ok = (Result<IReadOnlyList<(string Id, string Name, string Email)>, SqlError>.Ok<
            IReadOnlyList<(string Id, string Name, string Email)>,
            SqlError
        >)result;
        Assert.Equal(2, ok.Value.Count);
    }

    [Fact]
    public async Task Query_WithParameters_ReturnsFilteredResults()
    {
        await SetupDatabase().ConfigureAwait(false);

        var result = _connection.Query(
            sql: "SELECT Id, CustomerName FROM Customer WHERE CustomerName = @name",
            parameters: [new SqliteParameter("@name", "Acme Corp")],
            mapper: reader => reader.GetString(1)
        );

        Assert.True(
            result is Result<IReadOnlyList<string>, SqlError>.Ok<IReadOnlyList<string>, SqlError>
        );
        var ok = (Result<IReadOnlyList<string>, SqlError>.Ok<
            IReadOnlyList<string>,
            SqlError
        >)result;
        Assert.Single(ok.Value);
        Assert.Equal("Acme Corp", ok.Value[0]);
    }

    [Fact]
    public void Query_WithNullConnection_ReturnsError()
    {
        IDbConnection? nullConnection = null;

        var result = nullConnection!.Query<string>(sql: "SELECT 1");

        Assert.True(
            result is Result<IReadOnlyList<string>, SqlError>.Error<IReadOnlyList<string>, SqlError>
        );
    }

    [Fact]
    public async Task Query_WithEmptySql_ReturnsError()
    {
        await SetupDatabase().ConfigureAwait(false);

        var result = _connection.Query<string>(sql: "");

        Assert.True(
            result is Result<IReadOnlyList<string>, SqlError>.Error<IReadOnlyList<string>, SqlError>
        );
    }

    [Fact]
    public async Task Query_WithInvalidSql_ReturnsError()
    {
        await SetupDatabase().ConfigureAwait(false);

        var result = _connection.Query<string>(
            sql: "SELECT FROM INVALID_TABLE_THAT_DOES_NOT_EXIST",
            mapper: reader => reader.GetString(0)
        );

        Assert.True(
            result is Result<IReadOnlyList<string>, SqlError>.Error<IReadOnlyList<string>, SqlError>
        );
    }

    [Fact]
    public async Task Query_WithNullMapper_ReturnsEmptyResults()
    {
        await SetupDatabase().ConfigureAwait(false);

        var result = _connection.Query<string>(sql: "SELECT Id FROM Customer", mapper: null);

        Assert.True(
            result is Result<IReadOnlyList<string>, SqlError>.Ok<IReadOnlyList<string>, SqlError>
        );
        var ok = (Result<IReadOnlyList<string>, SqlError>.Ok<
            IReadOnlyList<string>,
            SqlError
        >)result;
        Assert.Empty(ok.Value);
    }

    #endregion

    #region DbConnectionExtensions.Execute

    [Fact]
    public async Task Execute_WithValidSql_ReturnsRowsAffected()
    {
        await SetupDatabase().ConfigureAwait(false);

        var result = _connection.Execute(
            sql: "UPDATE Customer SET Email = 'updated@test.com' WHERE CustomerName = 'Acme Corp'"
        );

        Assert.True(result is Result<int, SqlError>.Ok<int, SqlError>);
        var ok = (Result<int, SqlError>.Ok<int, SqlError>)result;
        Assert.Equal(1, ok.Value);
    }

    [Fact]
    public async Task Execute_WithParameters_ReturnsRowsAffected()
    {
        await SetupDatabase().ConfigureAwait(false);

        var result = _connection.Execute(
            sql: "UPDATE Customer SET Email = @email WHERE CustomerName = @name",
            parameters:
            [
                new SqliteParameter("@email", "new@test.com"),
                new SqliteParameter("@name", "Acme Corp"),
            ]
        );

        Assert.True(result is Result<int, SqlError>.Ok<int, SqlError>);
    }

    [Fact]
    public void Execute_WithNullConnection_ReturnsError()
    {
        IDbConnection? nullConnection = null;

        var result = nullConnection!.Execute(sql: "DELETE FROM Customer");

        Assert.True(result is Result<int, SqlError>.Error<int, SqlError>);
    }

    [Fact]
    public async Task Execute_WithEmptySql_ReturnsError()
    {
        await SetupDatabase().ConfigureAwait(false);

        var result = _connection.Execute(sql: "   ");

        Assert.True(result is Result<int, SqlError>.Error<int, SqlError>);
    }

    [Fact]
    public async Task Execute_WithInvalidSql_ReturnsError()
    {
        await SetupDatabase().ConfigureAwait(false);

        var result = _connection.Execute(sql: "INSERT INTO NonExistentTable VALUES (1)");

        Assert.True(result is Result<int, SqlError>.Error<int, SqlError>);
    }

    #endregion

    #region DbConnectionExtensions.Scalar

    [Fact]
    public async Task Scalar_WithValidSql_ReturnsValue()
    {
        await SetupDatabase().ConfigureAwait(false);

        var result = _connection.Scalar<object>(sql: "SELECT COUNT(*) FROM Customer");

        Assert.True(result is Result<object?, SqlError>.Ok<object?, SqlError>);
        var ok = (Result<object?, SqlError>.Ok<object?, SqlError>)result;
        Assert.Equal(2L, ok.Value);
    }

    [Fact]
    public async Task Scalar_WithParameters_ReturnsFilteredValue()
    {
        await SetupDatabase().ConfigureAwait(false);

        var result = _connection.Scalar<object>(
            sql: "SELECT COUNT(*) FROM Customer WHERE CustomerName = @name",
            parameters: [new SqliteParameter("@name", "Acme Corp")]
        );

        Assert.True(result is Result<object?, SqlError>.Ok<object?, SqlError>);
        var ok = (Result<object?, SqlError>.Ok<object?, SqlError>)result;
        Assert.Equal(1L, ok.Value);
    }

    [Fact]
    public void Scalar_WithNullConnection_ReturnsError()
    {
        IDbConnection? nullConnection = null;

        var result = nullConnection!.Scalar<object>(sql: "SELECT 1");

        Assert.True(result is Result<object?, SqlError>.Error<object?, SqlError>);
    }

    [Fact]
    public async Task Scalar_WithEmptySql_ReturnsError()
    {
        await SetupDatabase().ConfigureAwait(false);

        var result = _connection.Scalar<object>(sql: "");

        Assert.True(result is Result<object?, SqlError>.Error<object?, SqlError>);
    }

    [Fact]
    public async Task Scalar_WithInvalidSql_ReturnsError()
    {
        await SetupDatabase().ConfigureAwait(false);

        var result = _connection.Scalar<object>(sql: "SELECT FROM INVALID");

        Assert.True(result is Result<object?, SqlError>.Error<object?, SqlError>);
    }

    #endregion

    #region DbConnectionExtensions.GetRecords

    [Fact]
    public void GetRecords_WithNullConnection_ReturnsError()
    {
        IDbConnection? nullConnection = null;
        var statement = "Customer".From().SelectAll().ToSqlStatement();

        var result = nullConnection!.GetRecords(
            statement,
            stmt => stmt.ToSQLite(),
            reader => reader.GetString(0)
        );

        Assert.True(
            result is Result<IReadOnlyList<string>, SqlError>.Error<IReadOnlyList<string>, SqlError>
        );
    }

    [Fact]
    public async Task GetRecords_WithNullStatement_ReturnsError()
    {
        await SetupDatabase().ConfigureAwait(false);

        var result = _connection.GetRecords<string>(
            null!,
            stmt => stmt.ToSQLite(),
            reader => reader.GetString(0)
        );

        Assert.True(
            result is Result<IReadOnlyList<string>, SqlError>.Error<IReadOnlyList<string>, SqlError>
        );
    }

    [Fact]
    public async Task GetRecords_WithNullGenerator_ReturnsError()
    {
        await SetupDatabase().ConfigureAwait(false);
        var statement = "Customer".From().SelectAll().ToSqlStatement();

        var result = _connection.GetRecords<string>(
            statement,
            null!,
            reader => reader.GetString(0)
        );

        Assert.True(
            result is Result<IReadOnlyList<string>, SqlError>.Error<IReadOnlyList<string>, SqlError>
        );
    }

    [Fact]
    public async Task GetRecords_WithNullMapper_ReturnsError()
    {
        await SetupDatabase().ConfigureAwait(false);
        var statement = "Customer".From().SelectAll().ToSqlStatement();

        var result = _connection.GetRecords<string>(statement, stmt => stmt.ToSQLite(), null!);

        Assert.True(
            result is Result<IReadOnlyList<string>, SqlError>.Error<IReadOnlyList<string>, SqlError>
        );
    }

    #endregion

    #region DbTransact

    [Fact]
    public async Task Transact_VoidVersion_CommitsOnSuccess()
    {
        await SetupDatabase().ConfigureAwait(false);

        await _connection
            .Transact(async tx =>
            {
                var insertResult = tx.Execute(
                    sql: "INSERT INTO Customer (Id, CustomerName, Email, Phone, CreatedDate) VALUES ('cust-3', 'Test Corp', 'test@test.com', '555-0300', '2024-03-01')"
                );
                Assert.True(insertResult is Result<int, SqlError>.Ok<int, SqlError>);
                await Task.CompletedTask.ConfigureAwait(false);
            })
            .ConfigureAwait(false);

        var result = _connection.Query(
            sql: "SELECT COUNT(*) FROM Customer",
            mapper: reader => reader.GetInt64(0)
        );
        var ok = (Result<IReadOnlyList<long>, SqlError>.Ok<IReadOnlyList<long>, SqlError>)result;
        Assert.Equal(3L, ok.Value[0]);
    }

    [Fact]
    public async Task Transact_WithReturnValue_CommitsAndReturnsResult()
    {
        await SetupDatabase().ConfigureAwait(false);

        var result = await _connection
            .Transact(async tx =>
            {
                tx.Execute(
                    sql: "INSERT INTO Customer (Id, CustomerName, Email, Phone, CreatedDate) VALUES ('cust-4', 'Return Corp', 'return@test.com', '555-0400', '2024-04-01')"
                );
                await Task.CompletedTask.ConfigureAwait(false);
                return "success";
            })
            .ConfigureAwait(false);

        Assert.Equal("success", result);

        var countResult = _connection.Query(
            sql: "SELECT COUNT(*) FROM Customer",
            mapper: reader => reader.GetInt64(0)
        );
        var ok = (Result<IReadOnlyList<long>, SqlError>.Ok<
            IReadOnlyList<long>,
            SqlError
        >)countResult;
        Assert.Equal(3L, ok.Value[0]);
    }

    [Fact]
    public async Task Transact_OnException_RollsBack()
    {
        await SetupDatabase().ConfigureAwait(false);

        await Assert
            .ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _connection
                    .Transact(async tx =>
                    {
                        tx.Execute(
                            sql: "INSERT INTO Customer (Id, CustomerName, Email, Phone, CreatedDate) VALUES ('cust-rollback', 'Rollback Corp', 'roll@test.com', '555-0500', '2024-05-01')"
                        );
                        await Task.CompletedTask.ConfigureAwait(false);
                        throw new InvalidOperationException("Simulated failure");
                    })
                    .ConfigureAwait(false);
            })
            .ConfigureAwait(false);

        var countResult = _connection.Query(
            sql: "SELECT COUNT(*) FROM Customer",
            mapper: reader => reader.GetInt64(0)
        );
        var ok = (Result<IReadOnlyList<long>, SqlError>.Ok<
            IReadOnlyList<long>,
            SqlError
        >)countResult;
        Assert.Equal(2L, ok.Value[0]);
    }

    [Fact]
    public async Task Transact_WithReturnValue_OnException_RollsBack()
    {
        await SetupDatabase().ConfigureAwait(false);

        await Assert
            .ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _connection
                    .Transact<string>(async tx =>
                    {
                        tx.Execute(
                            sql: "INSERT INTO Customer (Id, CustomerName, Email, Phone, CreatedDate) VALUES ('cust-rb2', 'Rollback2 Corp', 'rb2@test.com', null, '2024-06-01')"
                        );
                        await Task.CompletedTask.ConfigureAwait(false);
                        throw new InvalidOperationException("Simulated failure");
                    })
                    .ConfigureAwait(false);
            })
            .ConfigureAwait(false);

        var countResult = _connection.Query(
            sql: "SELECT COUNT(*) FROM Customer",
            mapper: reader => reader.GetInt64(0)
        );
        var ok = (Result<IReadOnlyList<long>, SqlError>.Ok<
            IReadOnlyList<long>,
            SqlError
        >)countResult;
        Assert.Equal(2L, ok.Value[0]);
    }

    #endregion

    #region DbTransactionExtensions

    [Fact]
    public async Task TransactionQuery_WithValidSql_ReturnsResults()
    {
        await SetupDatabase().ConfigureAwait(false);

        await _connection
            .Transact(async tx =>
            {
                var result = tx.Query(
                    sql: "SELECT CustomerName FROM Customer ORDER BY CustomerName",
                    mapper: reader => reader.GetString(0)
                );

                Assert.True(
                    result
                        is Result<IReadOnlyList<string>, SqlError>.Ok<
                            IReadOnlyList<string>,
                            SqlError
                        >
                );
                var ok = (Result<IReadOnlyList<string>, SqlError>.Ok<
                    IReadOnlyList<string>,
                    SqlError
                >)result;
                Assert.Equal(2, ok.Value.Count);
                Assert.Equal("Acme Corp", ok.Value[0]);
                await Task.CompletedTask.ConfigureAwait(false);
            })
            .ConfigureAwait(false);
    }

    [Fact]
    public async Task TransactionQuery_WithParameters_ReturnsFilteredResults()
    {
        await SetupDatabase().ConfigureAwait(false);

        await _connection
            .Transact(async tx =>
            {
                var result = tx.Query(
                    sql: "SELECT CustomerName FROM Customer WHERE Email = @email",
                    parameters: [new SqliteParameter("@email", "contact@acme.com")],
                    mapper: reader => reader.GetString(0)
                );

                Assert.True(
                    result
                        is Result<IReadOnlyList<string>, SqlError>.Ok<
                            IReadOnlyList<string>,
                            SqlError
                        >
                );
                var ok = (Result<IReadOnlyList<string>, SqlError>.Ok<
                    IReadOnlyList<string>,
                    SqlError
                >)result;
                Assert.Single(ok.Value);
                await Task.CompletedTask.ConfigureAwait(false);
            })
            .ConfigureAwait(false);
    }

    [Fact]
    public void TransactionQuery_WithNullTransaction_ReturnsError()
    {
        IDbTransaction? nullTx = null;

        var result = nullTx!.Query<string>(sql: "SELECT 1");

        Assert.True(
            result is Result<IReadOnlyList<string>, SqlError>.Error<IReadOnlyList<string>, SqlError>
        );
    }

    [Fact]
    public async Task TransactionQuery_WithEmptySql_ReturnsError()
    {
        await SetupDatabase().ConfigureAwait(false);

        await _connection
            .Transact(async tx =>
            {
                var result = tx.Query<string>(sql: "");
                Assert.True(
                    result
                        is Result<IReadOnlyList<string>, SqlError>.Error<
                            IReadOnlyList<string>,
                            SqlError
                        >
                );
                await Task.CompletedTask.ConfigureAwait(false);
            })
            .ConfigureAwait(false);
    }

    [Fact]
    public async Task TransactionExecute_WithValidSql_ReturnsRowsAffected()
    {
        await SetupDatabase().ConfigureAwait(false);

        await _connection
            .Transact(async tx =>
            {
                var result = tx.Execute(
                    sql: "UPDATE Customer SET Phone = '555-9999' WHERE CustomerName = 'Acme Corp'"
                );
                Assert.True(result is Result<int, SqlError>.Ok<int, SqlError>);
                var ok = (Result<int, SqlError>.Ok<int, SqlError>)result;
                Assert.Equal(1, ok.Value);
                await Task.CompletedTask.ConfigureAwait(false);
            })
            .ConfigureAwait(false);
    }

    [Fact]
    public async Task TransactionExecute_WithParameters_ReturnsRowsAffected()
    {
        await SetupDatabase().ConfigureAwait(false);

        await _connection
            .Transact(async tx =>
            {
                var result = tx.Execute(
                    sql: "UPDATE Customer SET Phone = @phone WHERE CustomerName = @name",
                    parameters:
                    [
                        new SqliteParameter("@phone", "555-8888"),
                        new SqliteParameter("@name", "Tech Solutions"),
                    ]
                );
                Assert.True(result is Result<int, SqlError>.Ok<int, SqlError>);
                await Task.CompletedTask.ConfigureAwait(false);
            })
            .ConfigureAwait(false);
    }

    [Fact]
    public void TransactionExecute_WithNullTransaction_ReturnsError()
    {
        IDbTransaction? nullTx = null;

        var result = nullTx!.Execute(sql: "DELETE FROM Customer");

        Assert.True(result is Result<int, SqlError>.Error<int, SqlError>);
    }

    [Fact]
    public async Task TransactionExecute_WithEmptySql_ReturnsError()
    {
        await SetupDatabase().ConfigureAwait(false);

        await _connection
            .Transact(async tx =>
            {
                var result = tx.Execute(sql: "  ");
                Assert.True(result is Result<int, SqlError>.Error<int, SqlError>);
                await Task.CompletedTask.ConfigureAwait(false);
            })
            .ConfigureAwait(false);
    }

    [Fact]
    public async Task TransactionScalar_WithValidSql_ReturnsValue()
    {
        await SetupDatabase().ConfigureAwait(false);

        await _connection
            .Transact(async tx =>
            {
                var result = tx.Scalar<object>(sql: "SELECT COUNT(*) FROM Customer");
                Assert.True(result is Result<object?, SqlError>.Ok<object?, SqlError>);
                var ok = (Result<object?, SqlError>.Ok<object?, SqlError>)result;
                Assert.Equal(2L, ok.Value);
                await Task.CompletedTask.ConfigureAwait(false);
            })
            .ConfigureAwait(false);
    }

    [Fact]
    public async Task TransactionScalar_WithParameters_ReturnsValue()
    {
        await SetupDatabase().ConfigureAwait(false);

        await _connection
            .Transact(async tx =>
            {
                var result = tx.Scalar<object>(
                    sql: "SELECT COUNT(*) FROM Customer WHERE CustomerName = @name",
                    parameters: [new SqliteParameter("@name", "Acme Corp")]
                );
                Assert.True(result is Result<object?, SqlError>.Ok<object?, SqlError>);
                var ok = (Result<object?, SqlError>.Ok<object?, SqlError>)result;
                Assert.Equal(1L, ok.Value);
                await Task.CompletedTask.ConfigureAwait(false);
            })
            .ConfigureAwait(false);
    }

    [Fact]
    public void TransactionScalar_WithNullTransaction_ReturnsError()
    {
        IDbTransaction? nullTx = null;

        var result = nullTx!.Scalar<object>(sql: "SELECT 1");

        Assert.True(result is Result<object?, SqlError>.Error<object?, SqlError>);
    }

    [Fact]
    public async Task TransactionScalar_WithEmptySql_ReturnsError()
    {
        await SetupDatabase().ConfigureAwait(false);

        await _connection
            .Transact(async tx =>
            {
                var result = tx.Scalar<object>(sql: "");
                Assert.True(result is Result<object?, SqlError>.Error<object?, SqlError>);
                await Task.CompletedTask.ConfigureAwait(false);
            })
            .ConfigureAwait(false);
    }

    #endregion

    #region Config Types Coverage

    [Fact]
    public void DataProviderConfig_CanBeCreated()
    {
        var config = new Nimblesite.DataProvider.Core.DataProviderConfig
        {
            ConnectionString = "Data Source=test.db",
            Tables = new List<Nimblesite.DataProvider.Core.TableConfig>
            {
                new()
                {
                    Schema = "main",
                    Name = "users",
                    GenerateInsert = true,
                    GenerateUpdate = true,
                    GenerateDelete = true,
                    ExcludeColumns = new List<string> { "computed" }.AsReadOnly(),
                    PrimaryKeyColumns = new List<string> { "Id" }.AsReadOnly(),
                },
            }.AsReadOnly(),
        };

        Assert.NotNull(config.ConnectionString);
        Assert.Single(config.Tables);
        Assert.Equal("main", config.Tables[0].Schema);
        Assert.Equal("users", config.Tables[0].Name);
        Assert.True(config.Tables[0].GenerateInsert);
        Assert.True(config.Tables[0].GenerateUpdate);
        Assert.True(config.Tables[0].GenerateDelete);
        Assert.Single(config.Tables[0].ExcludeColumns);
        Assert.Single(config.Tables[0].PrimaryKeyColumns);
    }

    [Fact]
    public void DatabaseColumn_CanBeCreated()
    {
        var column = new Nimblesite.DataProvider.Core.DatabaseColumn
        {
            Name = "Id",
            SqlType = "TEXT",
            CSharpType = "string",
            IsNullable = false,
            IsPrimaryKey = true,
            IsIdentity = false,
            IsComputed = false,
            MaxLength = 50,
            Precision = 10,
            Scale = 2,
        };
        Assert.Equal("Id", column.Name);
        Assert.True(column.IsPrimaryKey);
        Assert.Equal(50, column.MaxLength);
    }

    [Fact]
    public void DatabaseTable_ComputedProperties_Work()
    {
        var table = new Nimblesite.DataProvider.Core.DatabaseTable
        {
            Schema = "main",
            Name = "TestTable",
            Columns = new List<Nimblesite.DataProvider.Core.DatabaseColumn>
            {
                new()
                {
                    Name = "Id",
                    SqlType = "TEXT",
                    CSharpType = "string",
                    IsPrimaryKey = true,
                },
                new()
                {
                    Name = "Name",
                    SqlType = "TEXT",
                    CSharpType = "string",
                },
                new()
                {
                    Name = "AutoId",
                    SqlType = "INTEGER",
                    CSharpType = "int",
                    IsIdentity = true,
                },
                new()
                {
                    Name = "Computed",
                    SqlType = "TEXT",
                    CSharpType = "string",
                    IsComputed = true,
                },
            }.AsReadOnly(),
        };

        Assert.Equal("main", table.Schema);
        Assert.Equal("TestTable", table.Name);
        Assert.Single(table.PrimaryKeyColumns);
        Assert.Equal("Id", table.PrimaryKeyColumns[0].Name);
        Assert.Equal(2, table.InsertableColumns.Count); // Id, Name (not AutoId, not Computed)
        Assert.Single(table.UpdateableColumns); // Name only (not PK, not Identity, not Computed)
    }

    [Fact]
    public void SqlQueryMetadata_CanBeCreated()
    {
        var metadata = new Nimblesite.DataProvider.Core.SqlQueryMetadata
        {
            SqlText = "SELECT * FROM Test",
            Columns = new List<Nimblesite.DataProvider.Core.DatabaseColumn>
            {
                new()
                {
                    Name = "Id",
                    SqlType = "TEXT",
                    CSharpType = "string",
                },
            }.AsReadOnly(),
        };

        Assert.Equal("SELECT * FROM Test", metadata.SqlText);
        Assert.Single(metadata.Columns);
    }

    [Fact]
    public void GroupingConfig_CanBeCreated()
    {
        var parent = new Nimblesite.DataProvider.Core.EntityConfig(
            Name: "Invoice",
            KeyColumns: new List<string> { "Id" }.AsReadOnly(),
            Columns: new List<string> { "Id", "InvoiceNumber" }.AsReadOnly()
        );
        var child = new Nimblesite.DataProvider.Core.EntityConfig(
            Name: "InvoiceLine",
            KeyColumns: new List<string> { "Id" }.AsReadOnly(),
            Columns: new List<string> { "Id", "InvoiceId" }.AsReadOnly(),
            ParentKeyColumns: new List<string> { "InvoiceId" }.AsReadOnly()
        );
        var config = new Nimblesite.DataProvider.Core.GroupingConfig(
            QueryName: "GetInvoices",
            GroupingStrategy: "ParentChild",
            ParentEntity: parent,
            ChildEntity: child
        );

        Assert.Equal("Invoice", config.ParentEntity.Name);
        Assert.Equal("GetInvoices", config.QueryName);
        Assert.NotNull(child.ParentKeyColumns);
    }

    [Fact]
    public void QueryConfigItem_CanBeCreated()
    {
        var item = new Nimblesite.DataProvider.Core.QueryConfigItem
        {
            Name = "GetInvoices",
            SqlFile = "GetInvoices.sql",
            GroupingFile = "GetInvoices.grouping.json",
        };
        Assert.Equal("GetInvoices", item.Name);
        Assert.Equal("GetInvoices.sql", item.SqlFile);
        Assert.Equal("GetInvoices.grouping.json", item.GroupingFile);
    }

    [Fact]
    public void TableConfigItem_CanBeCreated()
    {
        var item = new Nimblesite.DataProvider.Core.TableConfigItem
        {
            Name = "Invoice",
            Schema = "main",
            GenerateInsert = true,
            GenerateUpdate = true,
            GenerateDelete = false,
            ExcludeColumns = ["computed"],
            PrimaryKeyColumns = ["Id"],
        };
        Assert.Equal("Invoice", item.Name);
        Assert.Equal("main", item.Schema);
        Assert.True(item.GenerateInsert);
        Assert.Single(item.ExcludeColumns);
    }

    [Fact]
    public void SourceGeneratorConfig_CanBeCreated()
    {
        var config = new Nimblesite.DataProvider.Core.SourceGeneratorDataProviderConfiguration
        {
            ConnectionString = "test",
        };
        Assert.Equal("test", config.ConnectionString);
    }

    #endregion

    private async Task SetupDatabase()
    {
        await _connection.OpenAsync().ConfigureAwait(false);

        var createTablesScript = """
            CREATE TABLE IF NOT EXISTS Customer (
                Id TEXT PRIMARY KEY,
                CustomerName TEXT NOT NULL,
                Email TEXT NULL,
                Phone TEXT NULL,
                CreatedDate TEXT NOT NULL
            );
            """;

        using var command = new SqliteCommand(createTablesScript, _connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);

        var insertScript = """
            INSERT INTO Customer (Id, CustomerName, Email, Phone, CreatedDate) VALUES
            ('cust-1', 'Acme Corp', 'contact@acme.com', '555-0100', '2024-01-01'),
            ('cust-2', 'Tech Solutions', 'info@techsolutions.com', '555-0200', '2024-01-02');
            """;

        using var insertCommand = new SqliteCommand(insertScript, _connection);
        await insertCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public void Dispose() => DisposeSqliteFixture(connection: _connection, dbPath: _dbPath);

    /// <summary>
    /// Shared best-effort cleanup for SQLite file fixtures: disposes the connection and
    /// deletes the database file, tolerating a file locked by another process.
    /// </summary>
    internal static void DisposeSqliteFixture(SqliteConnection? connection, string dbPath)
    {
        connection?.Dispose();
        if (File.Exists(dbPath))
        {
            try
            {
                File.Delete(dbPath);
            }
#pragma warning disable CA1031 // Do not catch general exception types - file cleanup is best-effort
            catch (IOException)
            {
                /* File may be locked */
            }
#pragma warning restore CA1031
        }
    }
}
