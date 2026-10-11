// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.Sync.Http.Tests;

public partial class HttpMappingE2ETests
{
    private const string Data_SetupSqliteSource_1 = """
                CREATE TABLE User (
                    Id TEXT PRIMARY KEY,
                    FullName TEXT NOT NULL,
                    EmailAddress TEXT NOT NULL,
                    PasswordHash TEXT,
                    SecurityStamp TEXT,
                    CreatedAt TEXT NOT NULL
                );
                """;

    private const string Data_SetupPostgresTarget_1 = """
            DROP TABLE IF EXISTS customer CASCADE;
            CREATE TABLE customer (
                customer_id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                email TEXT NOT NULL,
                name_upper TEXT,
                source TEXT,
                registered_date TEXT
            );
            """;
}
