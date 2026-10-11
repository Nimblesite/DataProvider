using System.Collections.Immutable;
using System.Data;
using Generated;
using Microsoft.Data.Sqlite;
using Nimblesite.Sql.Model;
using Outcome;

namespace Nimblesite.Lql.TypeProvider.FSharp.Tests.Data;

/// <summary>
/// Seeds the test database with sample data using generated DataProvider extensions
/// </summary>
public static class TestDataSeeder
{
    /// <summary>
    /// Clears all test data from the database using generated delete extension
    /// </summary>
    /// <param name="transaction">The database transaction</param>
    /// <returns>Result indicating success or failure</returns>
    public static async Task<Result<int, SqlError>> ClearDataAsync(IDbTransaction transaction)
    {
        if (transaction.Connection is null)
            return new Result<int, SqlError>.Error<int, SqlError>(
                new SqlError("Transaction has no connection")
            );

        // Delete in order respecting foreign keys (Orders references Users)
        using (
            var cmd = new SqliteCommand(
                "DELETE FROM Orders",
                (SqliteConnection)transaction.Connection,
                (SqliteTransaction)transaction
            )
        )
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

        using (
            var cmd = new SqliteCommand(
                "DELETE FROM Users",
                (SqliteConnection)transaction.Connection,
                (SqliteTransaction)transaction
            )
        )
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

        using (
            var cmd = new SqliteCommand(
                "DELETE FROM Products",
                (SqliteConnection)transaction.Connection,
                (SqliteTransaction)transaction
            )
        )
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

        using (
            var cmd = new SqliteCommand(
                "DELETE FROM Customer",
                (SqliteConnection)transaction.Connection,
                (SqliteTransaction)transaction
            )
        )
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

        return new Result<int, SqlError>.Ok<int, SqlError>(0);
    }

    /// <summary>
    /// Seeds the database with test data using generated insert methods
    /// </summary>
    /// <param name="transaction">The database transaction</param>
    /// <returns>Result indicating success or failure</returns>
    public static async Task<Result<string, SqlError>> SeedDataAsync(IDbTransaction transaction)
    {
        // Clear existing data first, then run every fixture insert in dependency order.
        var steps = ImmutableArray.Create<Func<Task<Result<int, SqlError>>>>(
            () => ClearDataAsync(transaction),
            () =>
                transaction.InsertCustomerAsync("c1", "Acme Corp", "acme@example.com", 10, "active"),
            () =>
                transaction.InsertCustomerAsync("c2", "Tech Corp", "tech@example.com", 5, "active"),
            () => transaction.InsertCustomerAsync("c3", "New Corp", "new@example.com", 1, "pending"),
            () =>
                transaction.InsertUsersAsync(
                    "u1",
                    "Alice",
                    "alice@example.com",
                    30,
                    "active",
                    "admin",
                    "2024-01-01"
                ),
            () =>
                transaction.InsertUsersAsync(
                    "u2",
                    "Bob",
                    "bob@example.com",
                    16,
                    "active",
                    "user",
                    "2024-01-02"
                ),
            () =>
                transaction.InsertUsersAsync(
                    "u3",
                    "Charlie",
                    "charlie@example.com",
                    25,
                    "inactive",
                    "user",
                    "2024-01-03"
                ),
            () =>
                transaction.InsertUsersAsync(
                    "u4",
                    "Diana",
                    "diana@example.com",
                    15,
                    "active",
                    "admin",
                    "2024-01-04"
                ),
            () => transaction.InsertProductsAsync("p1", "Widget", 10.00, 100),
            () => transaction.InsertProductsAsync("p2", "Gadget", 25.50, 50),
            () => transaction.InsertProductsAsync("p3", "Gizmo", 5.00, 200),
            () =>
                transaction.InsertOrdersAsync(
                    "o1",
                    "u1",
                    "p1",
                    100.00,
                    90.00,
                    10.00,
                    0.00,
                    "completed"
                ),
            () =>
                transaction.InsertOrdersAsync(
                    "o2",
                    "u1",
                    "p2",
                    50.00,
                    45.00,
                    5.00,
                    0.00,
                    "completed"
                ),
            () =>
                transaction.InsertOrdersAsync(
                    "o3",
                    "u1",
                    "p1",
                    75.00,
                    68.00,
                    7.00,
                    0.00,
                    "pending"
                ),
            () =>
                transaction.InsertOrdersAsync(
                    "o4",
                    "u1",
                    "p3",
                    25.00,
                    22.50,
                    2.50,
                    0.00,
                    "completed"
                ),
            () =>
                transaction.InsertOrdersAsync(
                    "o5",
                    "u1",
                    "p2",
                    125.00,
                    112.50,
                    12.50,
                    0.00,
                    "completed"
                ),
            () =>
                transaction.InsertOrdersAsync(
                    "o6",
                    "u1",
                    "p1",
                    200.00,
                    180.00,
                    20.00,
                    0.00,
                    "pending"
                ),
            () =>
                transaction.InsertOrdersAsync(
                    "o7",
                    "u2",
                    "p3",
                    30.00,
                    27.00,
                    3.00,
                    0.00,
                    "completed"
                )
        );

        foreach (var step in steps)
        {
            var outcome = await step().ConfigureAwait(false);
            if (outcome is Result<int, SqlError>.Error<int, SqlError> error)
                return new Result<string, SqlError>.Error<string, SqlError>(error.Value);
        }

        return new Result<string, SqlError>.Ok<string, SqlError>("Test data seeded successfully");
    }
}
