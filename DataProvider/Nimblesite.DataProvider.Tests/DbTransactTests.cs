using Microsoft.Data.Sqlite;

#pragma warning disable CS1998 // This async method lacks 'await' operators and will run synchronously

namespace Nimblesite.DataProvider.Tests;

/// <summary>
/// Tests for DbTransact extension methods
/// </summary>
public sealed partial class DbTransactTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _connectionString;
    private readonly SqliteConnection _connection;

    public DbTransactTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"dbtransact_tests_{Guid.NewGuid()}.db");
        _connectionString = $"Data Source={_dbPath}";
        _connection = new SqliteConnection(_connectionString);
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
        var count = Convert.ToInt32(
            await selectCommand.ExecuteScalarAsync(),
            System.Globalization.CultureInfo.InvariantCulture
        );
        Assert.Equal(1, count);
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
                    using var command1 = new SqliteCommand(
                        "INSERT INTO TestTable (Id, Name) VALUES (@id, 'Test1')",
                        _connection,
                        tx as SqliteTransaction
                    );
                    command1.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                    await command1.ExecuteNonQueryAsync().ConfigureAwait(false);

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
        var count = Convert.ToInt32(
            await selectCommand.ExecuteScalarAsync(),
            System.Globalization.CultureInfo.InvariantCulture
        );
        Assert.Equal(0, count);
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
            using var command = new SqliteCommand(
                "INSERT INTO TestTable (Id, Name) VALUES (@id, 'Test1')",
                _connection,
                tx as SqliteTransaction
            );
            command.Parameters.AddWithValue("@id", testId);
            return await command.ExecuteNonQueryAsync().ConfigureAwait(false);
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
                    using var command = new SqliteCommand(
                        "INSERT INTO TestTable (Id, Name) VALUES (@id, 'Test1')",
                        _connection,
                        tx as SqliteTransaction
                    );
                    command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                    throw new InvalidOperationException("Test exception");
                })
                .ConfigureAwait(false);
        });

        // Assert - should be 0 because transaction was rolled back
        using var selectCommand = new SqliteCommand("SELECT COUNT(*) FROM TestTable", _connection);
        var count = Convert.ToInt32(
            await selectCommand.ExecuteScalarAsync(),
            System.Globalization.CultureInfo.InvariantCulture
        );
        Assert.Equal(0, count);
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
            using var command = new SqliteCommand(
                "INSERT INTO TestTable (Id, Name) VALUES (@id, 'Test1')",
                _connection,
                tx as SqliteTransaction
            );
            command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        });

        // Assert
        using var selectCommand = new SqliteCommand("SELECT COUNT(*) FROM TestTable", _connection);
        var count = Convert.ToInt32(
            await selectCommand.ExecuteScalarAsync(),
            System.Globalization.CultureInfo.InvariantCulture
        );
        Assert.Equal(1, count);
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

    [Fact]
    public async Task Transact_SqliteConnection_ThrowsOnNullBody()
    {
        // Arrange
        await _connection.OpenAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await _connection.Transact(null!).ConfigureAwait(false);
        });
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

    [Fact]
    public async Task Transact_SqliteConnection_WithReturnValue_ThrowsOnNullBody()
    {
        // Arrange
        await _connection.OpenAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await _connection.Transact(null!).ConfigureAwait(false);
        });
    }

    private async Task CreateTestTable(SqliteTransaction? transaction = null)
    {
        using var command = new SqliteCommand(
            Data_CreateTestTable_1,
            _connection,
            transaction
        );
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
}
