// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Tests;

public partial class LqlSqliteE2ETests
{
    private const string Data_CreateSchemaAndSeed_1 = """
        CREATE TABLE users (
            id TEXT PRIMARY KEY,
            name TEXT NOT NULL,
            email TEXT NOT NULL,
            age INTEGER NOT NULL,
            country TEXT NOT NULL,
            status TEXT NOT NULL DEFAULT 'active'
        );
        CREATE TABLE orders (
            id TEXT PRIMARY KEY,
            user_id TEXT NOT NULL,
            total REAL NOT NULL,
            status TEXT NOT NULL DEFAULT 'pending',
            FOREIGN KEY (user_id) REFERENCES users(id)
        );
        """;

    private const string Data_CreateSchemaAndSeed_2 = """
        INSERT INTO users VALUES ('u1', 'Alice', 'alice@test.com', 30, 'US', 'active');
        INSERT INTO users VALUES ('u2', 'Bob', 'bob@test.com', 25, 'UK', 'active');
        INSERT INTO users VALUES ('u3', 'Charlie', 'charlie@test.com', 45, 'US', 'inactive');
        INSERT INTO users VALUES ('u4', 'Diana', 'diana@test.com', 35, 'AU', 'active');
        INSERT INTO users VALUES ('u5', 'Eve', 'eve@test.com', 22, 'US', 'active');
        """;

    private const string Data_CreateSchemaAndSeed_3 = """
        INSERT INTO orders VALUES ('o1', 'u1', 150.00, 'completed');
        INSERT INTO orders VALUES ('o2', 'u1', 75.50, 'completed');
        INSERT INTO orders VALUES ('o3', 'u2', 200.00, 'pending');
        INSERT INTO orders VALUES ('o4', 'u3', 50.00, 'completed');
        INSERT INTO orders VALUES ('o5', 'u4', 300.00, 'shipped');
        INSERT INTO orders VALUES ('o6', 'u4', 125.00, 'completed');
        INSERT INTO orders VALUES ('o7', 'u5', 45.00, 'pending');
        """;
}
