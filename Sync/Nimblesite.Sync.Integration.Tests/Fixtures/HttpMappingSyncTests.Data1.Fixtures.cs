// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.Sync.Integration.Tests;

public partial class HttpMappingSyncTests
{
    private const string Data_CreateSourceDb_1 = """
                CREATE TABLE User (
                    Id TEXT PRIMARY KEY,
                    FullName TEXT NOT NULL,
                    EmailAddress TEXT,
                    PasswordHash TEXT,
                    SecurityStamp TEXT,
                    CreatedAt TEXT
                );
                """;

    private const string Data_CreateTargetDb_1 = """
            DROP TABLE IF EXISTS customer CASCADE;
            CREATE TABLE customer (
                customer_id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                email TEXT,
                source TEXT,
                registered_date TEXT
            );
            """;
}
