using System.Data;
using Microsoft.Data.Sqlite;
using Nimblesite.Lql.SQLite;
using Nimblesite.Sql.Model;
using Outcome;
using static global::Tests.Shared.ResultAssertions;

namespace Nimblesite.DataProvider.Example.Tests;

#pragma warning disable CS1591

/// <summary>
/// Tests for DataProvider.Core methods: DbConnectionExtensions, DbTransact, DbTransactionExtensions
/// </summary>
public sealed partial class CoreCoverageTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnection _connection;

    public CoreCoverageTests()
    {
        (_dbPath, _connection) = global::Tests.Shared.TempSqliteDatabase.CreateClosed(
            "core_coverage_tests"
        );
    }

    #region DbConnectionExtensions.Query

    [Fact]
    public async Task Query_WithValidSqlAndMapper_ReturnsResults()
    {
        await SetupDatabase().ConfigureAwait(false);

        var ok = Ok(read: () =>
            _connection.Query(
                sql: "SELECT Id, CustomerName, Email FROM Customer",
                mapper: reader =>
                    (Id: reader.GetString(0), Name: reader.GetString(1), Email: reader.GetString(2))
            )
        );
        Assert.Equal(2, ok.Value.Count);
    }

    [Fact]
    public async Task Query_WithParameters_ReturnsFilteredResults()
    {
        await SetupDatabase().ConfigureAwait(false);

        var ok = Ok(read: () =>
            _connection.Query(
                sql: "SELECT Id, CustomerName FROM Customer WHERE CustomerName = @name",
                parameters: [new SqliteParameter("@name", "Acme Corp")],
                mapper: reader => reader.GetString(1)
            )
        );
        Assert.Single(ok.Value);
        Assert.Equal("Acme Corp", ok.Value[0]);
    }

    [Fact]
    public void Query_WithNullConnection_ReturnsError()
    {
        IDbConnection? nullConnection = null;
        _ = Error(actual: nullConnection!.Query<string>(sql: "SELECT 1"));
    }

    [Fact]
    public async Task Query_WithEmptySql_ReturnsError()
    {
        await SetupDatabase().ConfigureAwait(false);

        var result = _connection.Query<string>(sql: "");

        _ = Error(actual: result);
    }

    [Fact]
    public async Task Query_WithInvalidSql_ReturnsError()
    {
        await SetupDatabase().ConfigureAwait(false);

        var result = _connection.Query<string>(
            sql: "SELECT FROM INVALID_TABLE_THAT_DOES_NOT_EXIST",
            mapper: reader => reader.GetString(0)
        );

        _ = Error(actual: result);
    }

    [Fact]
    public async Task Query_WithNullMapper_ReturnsEmptyResults()
    {
        await SetupDatabase().ConfigureAwait(false);

        var ok = Ok(read: () =>
            _connection.Query<string>(sql: "SELECT Id FROM Customer", mapper: null)
        );
        Assert.Empty(ok.Value);
    }

    #endregion

    #region DbConnectionExtensions.Execute

    [Fact]
    public async Task Execute_WithValidSql_ReturnsRowsAffected()
    {
        await SetupDatabase().ConfigureAwait(false);

        var ok = Ok(read: () =>
            _connection.Execute(
                sql: "UPDATE Customer SET Email = 'updated@test.com' WHERE CustomerName = 'Acme Corp'"
            )
        );
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

        _ = Ok(actual: result);
    }

    [Fact]
    public void Execute_WithNullConnection_ReturnsError()
    {
        IDbConnection? nullConnection = null;
        _ = Error(actual: nullConnection!.Execute(sql: "DELETE FROM Customer"));
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(IndependentScenarioData1))]
    public async Task Execute_WithEmptySql_ReturnsError_ScenarioCases(
        string scenarioName,
        System.String scenario1
    )
    {
        NamedCase(caseName: scenarioName);

        await SetupDatabase().ConfigureAwait(false);

        var result = _connection.Execute(sql: scenario1);

        _ = Error(actual: result);
    }

    #endregion

    #region DbConnectionExtensions.Scalar

    [Fact]
    public async Task Scalar_WithValidSql_ReturnsValue()
    {
        await SetupDatabase().ConfigureAwait(false);

        var ok = Ok(read: () => _connection.Scalar<object>(sql: "SELECT COUNT(*) FROM Customer"));
        Assert.Equal(2L, ok.Value);
    }

    [Fact]
    public async Task Scalar_WithParameters_ReturnsFilteredValue()
    {
        await SetupDatabase().ConfigureAwait(false);

        var ok = Ok(read: () =>
            _connection.Scalar<object>(
                sql: "SELECT COUNT(*) FROM Customer WHERE CustomerName = @name",
                parameters: [new SqliteParameter("@name", "Acme Corp")]
            )
        );
        Assert.Equal(1L, ok.Value);
    }

    [Fact]
    public void Scalar_WithNullConnection_ReturnsError()
    {
        IDbConnection? nullConnection = null;
        _ = Error(actual: nullConnection!.Scalar<object>(sql: "SELECT 1"));
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(IndependentScenarioData2))]
    public async Task Scalar_WithEmptySql_ReturnsError_ScenarioCases(
        string scenarioName,
        System.String scenario1
    )
    {
        NamedCase(caseName: scenarioName);

        await SetupDatabase().ConfigureAwait(false);

        var result = _connection.Scalar<object>(sql: scenario1);

        _ = Error(actual: result);
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

        _ = Error(actual: result);
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

        _ = Error(actual: result);
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

        _ = Error(actual: result);
    }

    [Fact]
    public async Task GetRecords_WithNullMapper_ReturnsError()
    {
        await SetupDatabase().ConfigureAwait(false);
        var statement = "Customer".From().SelectAll().ToSqlStatement();

        var result = _connection.GetRecords<string>(statement, stmt => stmt.ToSQLite(), null!);

        _ = Error(actual: result);
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
                _ = Ok(actual: insertResult);
                await Task.CompletedTask.ConfigureAwait(false);
            })
            .ConfigureAwait(false);

        VerifySharedChecks1(input0: _connection, value2: 3L);
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

        VerifySharedChecks1(input0: _connection, value2: 3L);
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX]: both rollback facts keep their
    // original names as case labels and share one exception/rollback workflow.
    [Theory]
    [InlineData(
        "Transact_OnException_RollsBack",
        false,
        "INSERT INTO Customer (Id, CustomerName, Email, Phone, CreatedDate) VALUES ('cust-rollback', 'Rollback Corp', 'roll@test.com', '555-0500', '2024-05-01')"
    )]
    [InlineData(
        "Transact_WithReturnValue_OnException_RollsBack",
        true,
        "INSERT INTO Customer (Id, CustomerName, Email, Phone, CreatedDate) VALUES ('cust-rb2', 'Rollback2 Corp', 'rb2@test.com', null, '2024-06-01')"
    )]
    public async Task Transact_OnException_RollsBack(
        string caseName,
        bool withReturnValue,
        string insertSql
    )
    {
        NamedCase(caseName: caseName);
        await SetupDatabase().ConfigureAwait(false);

        await Assert
            .ThrowsAsync<InvalidOperationException>(async () =>
            {
                if (withReturnValue)
                {
                    await _connection
                        .Transact<string>(async tx =>
                        {
                            await ThrowAfterInsert(transaction: tx, sql: insertSql)
                                .ConfigureAwait(false);
                            return string.Empty;
                        })
                        .ConfigureAwait(false);
                }
                else
                {
                    await _connection
                        .Transact(async tx =>
                            await ThrowAfterInsert(transaction: tx, sql: insertSql)
                                .ConfigureAwait(false)
                        )
                        .ConfigureAwait(false);
                }
            })
            .ConfigureAwait(false);

        VerifySharedChecks1(input0: _connection, value2: 2L);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS]: shared insert-then-fail body for the
    // rollback facts, preserving the exact insert statement and failure ordering.
    private static async Task ThrowAfterInsert(IDbTransaction transaction, string sql)
    {
        transaction.Execute(sql: sql);
        await Task.CompletedTask.ConfigureAwait(false);
        throw new InvalidOperationException("Simulated failure");
    }

    #endregion

    #region DbTransactionExtensions

    [Fact]
    public async Task TransactionQuery_WithValidSql_ReturnsResults() =>
        await InTransaction(body: tx =>
            {
                var ok = Ok(
                    actual: QueryStrings(
                        transaction: tx,
                        sql: "SELECT CustomerName FROM Customer ORDER BY CustomerName"
                    )
                );
                Assert.Equal(2, ok.Value.Count);
                Assert.Equal("Acme Corp", ok.Value[0]);
            })
            .ConfigureAwait(false);

    [Fact]
    public async Task TransactionQuery_WithParameters_ReturnsFilteredResults() =>
        await InTransaction(body: tx =>
            {
                var ok = Ok(read: () =>
                    tx.Query(
                        sql: "SELECT CustomerName FROM Customer WHERE Email = @email",
                        parameters: [new SqliteParameter("@email", "contact@acme.com")],
                        mapper: reader => reader.GetString(0)
                    )
                );
                Assert.Single(ok.Value);
            })
            .ConfigureAwait(false);

    [Fact]
    public void TransactionQuery_WithNullTransaction_ReturnsError()
    {
        IDbTransaction? nullTx = null;
        _ = Error(actual: nullTx!.Query<string>(sql: "SELECT 1"));
    }

    [Fact]
    public async Task TransactionQuery_WithEmptySql_ReturnsError() =>
        await InTransaction(body: tx =>
            {
                var result = tx.Query<string>(sql: "");
                _ = Error(actual: result);
            })
            .ConfigureAwait(false);

    [Fact]
    public async Task TransactionExecute_WithValidSql_ReturnsRowsAffected() =>
        await InTransaction(body: tx =>
            {
                var ok = Ok(read: () =>
                    tx.Execute(
                        sql: "UPDATE Customer SET Phone = '555-9999' WHERE CustomerName = 'Acme Corp'"
                    )
                );
                Assert.Equal(1, ok.Value);
            })
            .ConfigureAwait(false);

    [Fact]
    public async Task TransactionExecute_WithParameters_ReturnsRowsAffected() =>
        await InTransaction(body: tx =>
            {
                var result = tx.Execute(
                    sql: "UPDATE Customer SET Phone = @phone WHERE CustomerName = @name",
                    parameters:
                    [
                        new SqliteParameter("@phone", "555-8888"),
                        new SqliteParameter("@name", "Tech Solutions"),
                    ]
                );
                _ = Ok(actual: result);
            })
            .ConfigureAwait(false);

    [Fact]
    public void TransactionExecute_WithNullTransaction_ReturnsError()
    {
        IDbTransaction? nullTx = null;
        _ = Error(actual: nullTx!.Execute(sql: "DELETE FROM Customer"));
    }

    [Fact]
    public async Task TransactionExecute_WithEmptySql_ReturnsError() =>
        await InTransaction(body: tx =>
            {
                var result = tx.Execute(sql: "  ");
                _ = Error(actual: result);
            })
            .ConfigureAwait(false);

    [Fact]
    public async Task TransactionScalar_WithValidSql_ReturnsValue() =>
        await InTransaction(body: tx =>
            {
                var ok = Ok(read: () => tx.Scalar<object>(sql: "SELECT COUNT(*) FROM Customer"));
                Assert.Equal(2L, ok.Value);
            })
            .ConfigureAwait(false);

    [Fact]
    public async Task TransactionScalar_WithParameters_ReturnsValue() =>
        await InTransaction(body: tx =>
            {
                var ok = Ok(read: () =>
                    tx.Scalar<object>(
                        sql: "SELECT COUNT(*) FROM Customer WHERE CustomerName = @name",
                        parameters: [new SqliteParameter("@name", "Acme Corp")]
                    )
                );
                Assert.Equal(1L, ok.Value);
            })
            .ConfigureAwait(false);

    [Fact]
    public void TransactionScalar_WithNullTransaction_ReturnsError()
    {
        IDbTransaction? nullTx = null;
        _ = Error(actual: nullTx!.Scalar<object>(sql: "SELECT 1"));
    }

    [Fact]
    public async Task TransactionScalar_WithEmptySql_ReturnsError() =>
        await InTransaction(body: tx =>
            {
                var result = tx.Scalar<object>(sql: "");
                _ = Error(actual: result);
            })
            .ConfigureAwait(false);

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
        var column = Dataset_DatabaseColumn_CanBeCreated_1();
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
            Columns = Dataset_DatabaseTable_ComputedProperties_Work_1().AsReadOnly(),
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
            Columns = Dataset_SqlQueryMetadata_CanBeCreated_1().AsReadOnly(),
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
        var config = Dataset_GroupingConfig_CanBeCreated_1(parent: parent, child: child);

        Assert.Equal("Invoice", config.ParentEntity.Name);
        Assert.Equal("GetInvoices", config.QueryName);
        Assert.NotNull(child.ParentKeyColumns);
    }

    [Fact]
    public void QueryConfigItem_CanBeCreated()
    {
        var item = Dataset_QueryConfigItem_CanBeCreated_1();
        Assert.Equal("GetInvoices", item.Name);
        Assert.Equal("GetInvoices.sql", item.SqlFile);
        Assert.Equal("GetInvoices.grouping.json", item.GroupingFile);
    }

    [Fact]
    public void TableConfigItem_CanBeCreated()
    {
        var item = Dataset_TableConfigItem_CanBeCreated_1();
        Assert.Equal("Invoice", item.Name);
        Assert.Equal("main", item.Schema);
        Assert.True(item.GenerateInsert);
        Assert.Single(item.ExcludeColumns);
    }

    [Fact]
    public void SourceGeneratorConfig_CanBeCreated()
    {
        var config = Dataset_SourceGeneratorConfig_CanBeCreated_1();
        Assert.Equal("test", config.ConnectionString);
    }

    #endregion

    private async Task SetupDatabase()
    {
        await _connection.OpenAsync().ConfigureAwait(false);

        await VerifySharedPhase1(
                configure0: command => command.CommandText = Data_SetupDatabase_1,
                configure1: command => command.CommandText = Data_SetupDatabase_2,
                input0: _connection
            )
            .ConfigureAwait(false);
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

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void VerifySharedChecks1(SqliteConnection input0, long value2)
    {
        var ok = Ok(read: () =>
            input0.Query(sql: "SELECT COUNT(*) FROM Customer", mapper: reader => reader.GetInt64(0))
        );
        Assert.Equal(value2, ok.Value[0]);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS]: one-column string projection for a
    // transaction receiver, shared by every generated-operation lookup.
    internal static Result<IReadOnlyList<string>, SqlError> QueryStrings(
        IDbTransaction transaction,
        string sql
    ) => transaction.Query(sql: sql, mapper: reader => reader.GetString(0));

    // Implements [CI-TEST-SHARED-SCENARIOS]: runs a synchronous body against a
    // transaction created after the shared database setup, preserving the
    // original commit/rollback lifetime for every transaction receiver test.
    private async Task InTransaction(Action<IDbTransaction> body)
    {
        await SetupDatabase().ConfigureAwait(false);

        await _connection
            .Transact(async tx =>
            {
                body(obj: tx);
                await Task.CompletedTask.ConfigureAwait(false);
            })
            .ConfigureAwait(false);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS]: unwraps the one-column projection value.
    internal static IReadOnlyList<string> QueryStringList(IDbTransaction transaction, string sql) =>
        Ok(actual: QueryStrings(transaction: transaction, sql: sql)).Value;

    // Implements [CI-TEST-SHARED-SCENARIOS]: two-column identifier projection for a
    // transaction receiver, shared by every generated update/lookup pair.
    internal static (string Id, string Reference) SelectIdAndReference(
        IDbTransaction transaction,
        string sql
    ) =>
        Ok(read: () =>
            transaction.Query(
                sql: sql,
                mapper: reader => (Id: reader.GetString(0), Reference: reader.GetString(1))
            )
        ).Value[0];

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static async global::System.Threading.Tasks.Task VerifySharedPhase1(
        SqliteConnection input0,
        global::System.Action<global::Microsoft.Data.Sqlite.SqliteCommand> configure0,
        global::System.Action<global::Microsoft.Data.Sqlite.SqliteCommand> configure1
    )
    {
        await global::Nimblesite
            .DataProvider.Example.Tests.GeneratedOperationsCoverageTests.VerifyUnifiedWorkflow101(
                configure: configure0,
                input1: input0
            )
            .ConfigureAwait(false);

        await global::Nimblesite
            .DataProvider.Example.Tests.GeneratedOperationsCoverageTests.VerifyUnifiedWorkflow101(
                configure: configure1,
                input1: input0
            )
            .ConfigureAwait(false);
    }
}
