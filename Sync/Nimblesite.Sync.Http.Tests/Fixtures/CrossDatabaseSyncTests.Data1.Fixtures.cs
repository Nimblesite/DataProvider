// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.Sync.Http.Tests;

public partial class CrossDatabaseSyncTests
{
    private const string Data_CreateSqliteDb_1 = """
                CREATE TABLE Person (
                    Id TEXT PRIMARY KEY,
                    Name TEXT NOT NULL,
                    Email TEXT
                );
                """;

    private const string Data_CreatePostgresDb_1 = """
            DROP TABLE IF EXISTS person CASCADE;
            CREATE TABLE person (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                email TEXT
            );
            """;
}
