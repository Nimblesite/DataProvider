// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.Sync.SQLite.Tests;

public partial class SyncRepositoryIntegrationTests
{
    private const string Data_Shared_1 = """
                CREATE TABLE Person (
                    Id TEXT PRIMARY KEY,
                    Name TEXT NOT NULL,
                    Age INTEGER
                );
                """;
}
