// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.Sync.Postgres.Tests;

public partial class CrossDatabaseSyncTests
{
    private const string Data_CreateTestTable_1 = """
                CREATE TABLE IF NOT EXISTS person (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    email TEXT
                )
                """;

    private const string Data_CreateTestTable_2 = """
                CREATE TABLE IF NOT EXISTS Person (
                    Id TEXT PRIMARY KEY,
                    Name TEXT NOT NULL,
                    Email TEXT
                )
                """;
}
