#pragma warning disable CS1998 // This async method lacks 'await' operators and will run synchronously

namespace Nimblesite.DataProvider.Tests;

/// <summary>
/// Tests for DbTransact extension methods
/// </summary>
public sealed partial class DbTransactTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnection _connection;

    public DbTransactTests()
    {
        (_dbPath, _connection) = global::Tests.Shared.TempSqliteDatabase.CreateClosed(
            "dbtransact_tests"
        );
    }

    [Fact]
    public async Task Transact_SqliteConnection_CommitsSuccessfulTransaction()
    {
        // Arrange
        await _connection.OpenAsync();
        await CreateTestTable();

        // Act
        var testId = Guid.NewGuid().ToString();
        await _connection.Transact(async tx =>
        {
            using var command = new SqliteCommand(
                "INSERT INTO TestTable (Id, Name) VALUES (@id, 'Test1')",
                _connection,
                tx as SqliteTransaction
            );
            command.Parameters.AddWithValue("@id", testId);
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        });

        // Assert
        using var selectCommand = new SqliteCommand("SELECT COUNT(*) FROM TestTable", _connection);
        await VerifySharedChecks3(input0: selectCommand, value0: 1);
    }

    [Fact]
    public async Task Transact_SqliteConnection_RollsBackFailedTransaction()
    {
        // Arrange
        await _connection.OpenAsync();
        await CreateTestTable();

        // Act & Assert
        await Assert.ThrowsAsync<SqliteException>(async () =>
        {
            await _connection
                .Transact(async tx =>
                {
                    await InsertTestRow(tx: tx, id: Guid.NewGuid().ToString())
                        .ConfigureAwait(false);

                    // This will fail because InvalidTable doesn't exist
                    using var command2 = new SqliteCommand(
                        "INSERT INTO InvalidTable (Name) VALUES ('Test2')",
                        _connection,
                        tx as SqliteTransaction
                    );
                    await command2.ExecuteNonQueryAsync().ConfigureAwait(false);
                })
                .ConfigureAwait(false);
        });

        // Assert - should be 0 because transaction was rolled back
        using var selectCommand = new SqliteCommand("SELECT COUNT(*) FROM TestTable", _connection);
        await VerifySharedChecks3(input0: selectCommand, value0: 0);
    }

    [Fact]
    public async Task Transact_SqliteConnection_WithReturnValue_ReturnsCorrectValue()
    {
        // Arrange
        await _connection.OpenAsync();
        await CreateTestTable();

        // Act
        var testId = Guid.NewGuid().ToString();
        var result = await _connection.Transact(async tx =>
        {
            return await InsertTestRow(tx: tx, id: testId).ConfigureAwait(false);
        });

        // Assert - 1 row affected
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task Transact_SqliteConnection_WithReturnValue_RollsBackOnException()
    {
        // Arrange
        await _connection.OpenAsync();
        await CreateTestTable();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _connection
                .Transact<int>(async tx =>
                {
                    await InsertTestRow(tx: tx, id: Guid.NewGuid().ToString())
                        .ConfigureAwait(false);

                    throw new InvalidOperationException("Test exception");
                })
                .ConfigureAwait(false);
        });

        // Assert - should be 0 because transaction was rolled back
        using var selectCommand = new SqliteCommand("SELECT COUNT(*) FROM TestTable", _connection);
        await VerifySharedChecks3(input0: selectCommand, value0: 0);
    }

    [Fact]
    public async Task Transact_SqliteConnection_OpensConnectionIfClosed()
    {
        // Arrange
        // Connection starts closed

        // Act
        await _connection.Transact(async tx =>
        {
            await CreateTestTable(tx as SqliteTransaction).ConfigureAwait(false);
            await InsertTestRow(tx: tx, id: Guid.NewGuid().ToString()).ConfigureAwait(false);
        });

        // Assert
        using var selectCommand = new SqliteCommand("SELECT COUNT(*) FROM TestTable", _connection);
        await VerifySharedChecks3(input0: selectCommand, value0: 1);
    }

    [Fact]
    public async Task Transact_SqliteConnection_ThrowsOnNullConnection()
    {
        // Arrange
        SqliteConnection nullConnection = null!;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await nullConnection.Transact(async tx => { }).ConfigureAwait(false);
        });
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(Transact_SqliteConnection_ThrowsOnNullBody_ContextCasesData))]
    public Task Transact_SqliteConnection_ThrowsOnNullBody_ContextCases(string caseName)
    {
        Assert.False(condition: string.IsNullOrEmpty(value: caseName));
        return ExerciseProjectionCaseMatrix2(input0: _connection);
    }

    [Fact]
    public async Task Transact_SqliteConnection_WithReturnValue_ThrowsOnNullConnection()
    {
        // Arrange
        SqliteConnection nullConnection = null!;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await nullConnection.Transact(async tx => 42).ConfigureAwait(false);
        });
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private async Task<int> InsertTestRow(System.Data.IDbTransaction tx, string id)
    {
        using var command = new SqliteCommand(
            "INSERT INTO TestTable (Id, Name) VALUES (@id, 'Test1')",
            _connection,
            tx as SqliteTransaction
        );
        command.Parameters.AddWithValue("@id", id);
        return await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private async Task CreateTestTable(SqliteTransaction? transaction = null)
    {
        using var command = new SqliteCommand(Data_CreateTestTable_1, _connection, transaction);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
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

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static async global::System.Threading.Tasks.Task VerifySharedChecks3(
        SqliteCommand input0,
        int value0
    )
    {
        var count = Convert.ToInt32(
            await input0.ExecuteScalarAsync().ConfigureAwait(false),
            System.Globalization.CultureInfo.InvariantCulture
        );
        Assert.Equal(value0, count);
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    private static async Task ExerciseProjectionCaseMatrix2(SqliteConnection input0)
    { // Arrange
        await (input0.OpenAsync()).ConfigureAwait(continueOnCapturedContext: true);

        // Act & Assert
        await (
            Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await input0.Transact(null!).ConfigureAwait(false);
            })
        ).ConfigureAwait(continueOnCapturedContext: true);
    }
}
