// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.Sync.Http.Tests;

public partial class HttpSyncE2ETests
{
    private const string Data_InitializeDatabase_1 = """
                CREATE TABLE Person (
                    Id TEXT PRIMARY KEY,
                    Name TEXT NOT NULL,
                    Email TEXT
                );
                """;
}
