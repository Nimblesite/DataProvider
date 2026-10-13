#pragma warning disable CA1861 // Prefer static readonly fields
#pragma warning disable CA2000 // Dispose objects before losing scope
#pragma warning disable CA1849 // Synchronous blocking calls

namespace Nimblesite.DataProvider.Tests.Fakes;

public class FakeDbConnectionTests
{
    [Fact]
    public void FakeDbConnection_ActsAsFactoryForFakeTransaction()
    {
        // Arrange
        var connection = new FakeDbConnection(sql => UserReader(name: "Test"));

        // Act
        connection.Open();
        var transaction = connection.BeginTransaction();

        // Assert
        Assert.NotNull(transaction);
        Assert.IsType<FakeTransaction>(transaction);
        Assert.Equal(connection, transaction.Connection);
    }

    [Fact]
    public void FakeTransaction_UsesCallbackToReturnDataBasedOnSelectStatement()
    {
        // Arrange - Using a match/case table to return different data based on SQL
        var connection = ConnectionFor(
            (
                "SELECT * FROM Users",
                new FakeDataReader(
                    ["Id", "Name", "Email"],
                    [typeof(int), typeof(string), typeof(string)],
                    [
                        [1, "John Doe", "john@example.com"],
                        [2, "Jane Smith", "jane@example.com"],
                    ]
                )
            ),
            (
                "SELECT * FROM Posts",
                new FakeDataReader(
                    ["Id", "Title", "UserId"],
                    [typeof(int), typeof(string), typeof(int)],
                    [
                        [1, "First Post", 1],
                        [2, "Second Post", 2],
                    ]
                )
            ),
            (
                "SELECT COUNT(*) FROM Users",
                new FakeDataReader(
                    ["Count"],
                    [typeof(int)],
                    [
                        [2],
                    ]
                )
            )
        );

        // Act & Assert - Test Users query
        ReadRows(
            connection: connection,
            sql: "SELECT * FROM Users",
            assert: usersReader =>
            {
                Assert.True(usersReader.Read());
                Assert.Equal(1, usersReader.GetInt32(0));
                Assert.Equal("John Doe", usersReader.GetString(1));
                Assert.Equal("john@example.com", usersReader.GetString(2));

                Assert.True(usersReader.Read());
                Assert.Equal(2, usersReader.GetInt32(0));
                Assert.Equal("Jane Smith", usersReader.GetString(1));
                Assert.Equal("jane@example.com", usersReader.GetString(2));

                Assert.False(usersReader.Read());
            }
        );
    }

    [Fact]
    public void FakeTransaction_HandlesCountQuery()
    {
        // Arrange
        var connection = ConnectionFor(
            (
                "SELECT COUNT(*) FROM Users",
                new FakeDataReader(
                    ["Count"],
                    [typeof(int)],
                    [
                        [42],
                    ]
                )
            )
        );

        // Act & Assert
        ReadRows(
            connection: connection,
            sql: "SELECT COUNT(*) FROM Users",
            assert: reader =>
            {
                Assert.True(reader.Read());
                Assert.Equal(42, reader.GetInt32(0));
                Assert.False(reader.Read());
            }
        );
    }

    [Fact]
    public void FakeCommand_UsesDataReaderFactory()
    {
        // Arrange & Act
        using var command = CreateTestUserCommand();
        using var reader = command.ExecuteReader();

        // Assert
        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32(0));
        Assert.Equal("Test User", reader.GetString(1));
    }

    [Fact]
    public void FakeDataReader_HandlesNullValues()
    {
        // Arrange
        var connection = new FakeDbConnection(sql => new FakeDataReader(
            ["Id", "Name", "Email"],
            [typeof(int), typeof(string), typeof(string)],
            [
                [1, "John Doe", DBNull.Value],
            ]
        ));

        // Act & Assert
        ReadRows(
            connection: connection,
            sql: "SELECT * FROM Users",
            assert: reader =>
            {
                Assert.True(reader.Read());
                Assert.Equal(1, reader.GetInt32(0));
                Assert.Equal("John Doe", reader.GetString(1));
                Assert.True(reader.IsDBNull(2));
            }
        );
    }

    [Fact]
    public async Task FakeDataReader_SupportsAsyncOperations()
    {
        // Arrange & Act
        using var command = CreateTestUserCommand();
        using var reader = await command.ExecuteReaderAsync();

        // Assert
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, reader.GetInt32(0));
        Assert.Equal("Test User", reader.GetString(1));
        Assert.False(await reader.IsDBNullAsync(0));
    }

    // Implements [CI-TEST-SHARED-SCENARIOS]: opens the fake connection, runs the
    // query through a fake transaction and asserts on the returned reader.
    private static void ReadRows(
        FakeDbConnection connection,
        string sql,
        Action<DbDataReader> assert
    )
    {
        connection.Open();
        using var transaction = (FakeTransaction)connection.BeginTransaction();
        using var reader = transaction.GetDataReader(sql);
        assert(reader);
    }

    private static DbCommand CreateTestUserCommand()
    {
        var connection = new FakeDbConnection(sql => UserReader(name: "Test User"));

        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Users";
        return command;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS]: builds a connection from a
    // match/reader table, falling back to a fixed "Unknown query" reader.
    private static FakeDbConnection ConnectionFor(
        params (string Match, FakeDataReader Reader)[] cases
    ) =>
        new(sql =>
            cases.FirstOrDefault(c => sql.Contains(c.Match, StringComparison.Ordinal)).Reader
            ?? new FakeDataReader(
                ["Error"],
                [typeof(string)],
                [
                    ["Unknown query"],
                ]
            )
        );

    private static FakeDataReader UserReader(string name) =>
        new(
            ["Id", "Name"],
            [typeof(int), typeof(string)],
            [
                [1, name],
            ]
        );
}
