// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SqliteMigrationTests
{
    private const string Data_SchemaCapture_ExistingDatabase_ReturnsCompleteSchema_1 = """
        CREATE TABLE customers (
            id TEXT PRIMARY KEY,
            email TEXT NOT NULL,
            name TEXT,
            created_at TEXT DEFAULT CURRENT_TIMESTAMP
        );
        CREATE UNIQUE INDEX idx_customers_email ON customers(email);
        CREATE TABLE orders (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            customer_id TEXT NOT NULL,
            total REAL,
            FOREIGN KEY (customer_id) REFERENCES customers(id) ON DELETE CASCADE
        );
        CREATE INDEX idx_orders_customer ON orders(customer_id);
        """;

    private const string Data_SchemaCapture_SerializesToJson_RoundTrip_1 = """
        CREATE TABLE products (
            id TEXT PRIMARY KEY,
            name TEXT NOT NULL,
            price REAL,
            active INTEGER DEFAULT 1
        );
        CREATE INDEX idx_products_name ON products(name);
        """;

    private const string Data_ExpressionIndex_SchemaInspector_DetectsExpressionIndex_1 = """
        CREATE TABLE artists (
            id TEXT PRIMARY KEY,
            name TEXT NOT NULL
        );
        CREATE UNIQUE INDEX uq_artists_name ON artists(lower(name));
        """;
}
