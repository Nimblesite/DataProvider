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
public static partial class TestDataSeeder
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

        // Implements [LQL-FSHARP-SHARED-CLEAR-DATA]. Orders must be deleted first.
        var connection = (SqliteConnection)transaction.Connection;
        var sqliteTransaction = (SqliteTransaction)transaction;
        ImmutableArray<Func<SqliteCommand>> commands =
        [
            () => new SqliteCommand("DELETE FROM Orders", connection, sqliteTransaction),
            () => new SqliteCommand("DELETE FROM Users", connection, sqliteTransaction),
            () => new SqliteCommand("DELETE FROM Products", connection, sqliteTransaction),
            () => new SqliteCommand("DELETE FROM Customer", connection, sqliteTransaction),
        ];
        foreach (var createCommand in commands)
        {
            using var command = createCommand();
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

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
        ImmutableArray<Func<Task<Result<int, SqlError>>>> steps =
        [
            () => ClearDataAsync(transaction),
            () =>
                transaction.InsertCustomerAsync(
                    "c1",
                    "Acme Corp",
                    "acme@example.com",
                    10,
                    "active"
                ),
            () =>
                transaction.InsertCustomerAsync("c2", "Tech Corp", "tech@example.com", 5, "active"),
            () =>
                transaction.InsertCustomerAsync("c3", "New Corp", "new@example.com", 1, "pending"),
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
            .. OrderSteps(transaction: transaction),
        ];

        foreach (var step in steps)
        {
            var outcome = await step().ConfigureAwait(false);
            if (outcome is Result<int, SqlError>.Error<int, SqlError> error)
                return new Result<string, SqlError>.Error<string, SqlError>(error.Value);
        }

        return new Result<string, SqlError>.Ok<string, SqlError>("Test data seeded successfully");
    }

    // Implements [LQL-FSHARP-SHARED-SEED-ORDERS].
    private static IEnumerable<Func<Task<Result<int, SqlError>>>> OrderSteps(
        IDbTransaction transaction
    )
    {
        foreach (var order in OrderInputs())
        {
            yield return () =>
                transaction.InsertOrdersAsync(
                    id: order.Id,
                    userid: order.User,
                    productid: order.Product,
                    total: order.Total,
                    subtotal: order.Subtotal,
                    tax: order.Tax,
                    discount: 0.00,
                    status: order.Status
                );
        }
    }
}
