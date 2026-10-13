using Microsoft.Data.Sqlite;

namespace Nimblesite.DataProvider.Example;

/// <summary>
/// Manages database connections and complete initialization including schema creation and data seeding
/// </summary>
internal static class DatabaseManager
{
    private const string ConnectionString = "Data Source=invoices.db";

    /// <summary>
    /// Initializes a fresh database with schema and sample data
    /// </summary>
    /// <returns>An open, initialized database connection</returns>
    public static async Task<SqliteConnection> InitializeAsync()
    {
        // Delete existing database to ensure clean start
#pragma warning disable RS1035 // Do not use File in analyzers - this is application code
        if (File.Exists("invoices.db"))
        {
            File.Delete("invoices.db");
        }
#pragma warning restore RS1035

        var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        // Try to force 32-bit integer mode
        using (var pragmaCmd = new SqliteCommand("PRAGMA legacy_alter_table = ON", connection))
        {
            await pragmaCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        await CreateSchemaAsync(connection).ConfigureAwait(false);
        await ClearExistingDataAsync(connection).ConfigureAwait(false);

        var insertResult = await connection
            .Transact(
                async (transaction) =>
                {
                    (bool flowControl, StringSqlResult value) = await SampleDataSeeder
                        .SeedDataAsync(transaction)
                        .ConfigureAwait(false);
                    if (!flowControl)
                    {
                        return value;
                    }

                    return new StringSqlOk("Data inserted successfully using generated methods");
                }
            )
            .ConfigureAwait(false);

        return connection;
    }

    /// <summary>
    /// Creates all necessary database tables and schema
    /// </summary>
    /// <param name="connection">The SQLite connection to create tables in</param>
    // Implements [EXAMPLE-SCHEMA-SHARED].
    internal static async Task CreateSchemaAsync(SqliteConnection connection)
    {
        using var command = new SqliteCommand(
            """
            CREATE TABLE IF NOT EXISTS Invoice (
                Id TEXT PRIMARY KEY,
                InvoiceNumber TEXT NOT NULL,
                InvoiceDate TEXT NOT NULL,
                CustomerName TEXT NOT NULL,
                CustomerEmail TEXT NULL,
                TotalAmount REAL NOT NULL,
                DiscountAmount REAL NULL,
                Notes TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS InvoiceLine (
                Id TEXT PRIMARY KEY,
                InvoiceId TEXT NOT NULL,
                Description TEXT NOT NULL,
                Quantity REAL NOT NULL,
                UnitPrice REAL NOT NULL,
                Amount REAL NOT NULL,
                DiscountPercentage REAL NULL,
                Notes TEXT NULL,
                FOREIGN KEY (InvoiceId) REFERENCES Invoice (Id)
            );

            CREATE TABLE IF NOT EXISTS Customer (
                Id TEXT PRIMARY KEY,
                CustomerName TEXT NOT NULL,
                Email TEXT NULL,
                Phone TEXT NULL,
                CreatedDate TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Address (
                Id TEXT PRIMARY KEY,
                CustomerId TEXT NOT NULL,
                Street TEXT NOT NULL,
                City TEXT NOT NULL,
                State TEXT NOT NULL,
                ZipCode TEXT NOT NULL,
                Country TEXT NOT NULL,
                FOREIGN KEY (CustomerId) REFERENCES Customer (Id)
            );

            CREATE TABLE IF NOT EXISTS Orders (
                Id TEXT PRIMARY KEY,
                OrderNumber TEXT NOT NULL,
                OrderDate TEXT NOT NULL,
                CustomerId TEXT NOT NULL,
                TotalAmount REAL NOT NULL,
                Status TEXT NOT NULL,
                FOREIGN KEY (CustomerId) REFERENCES Customer (Id)
            );

            CREATE TABLE IF NOT EXISTS OrderItem (
                Id TEXT PRIMARY KEY,
                OrderId TEXT NOT NULL,
                ProductName TEXT NOT NULL,
                Quantity REAL NOT NULL,
                Price REAL NOT NULL,
                Subtotal REAL NOT NULL,
                FOREIGN KEY (OrderId) REFERENCES Orders (Id)
            );
            """,
            connection
        );
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Clears all existing data from the database tables
    /// </summary>
    /// <param name="connection">The database connection</param>
    private static async Task ClearExistingDataAsync(SqliteConnection connection)
    {
        using var command = new SqliteCommand(
            "DELETE FROM OrderItem; DELETE FROM Orders; DELETE FROM Address; DELETE FROM Customer; DELETE FROM InvoiceLine; DELETE FROM Invoice;",
            connection
        );
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
