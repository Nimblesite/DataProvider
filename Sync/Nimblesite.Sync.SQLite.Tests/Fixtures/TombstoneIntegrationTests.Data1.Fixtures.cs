// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.Sync.SQLite.Tests;

public partial class TombstoneIntegrationTests
{
    private const string Data_CreatePersonTable_1 = """
                CREATE TABLE Person (
                    Id TEXT PRIMARY KEY,
                    Name TEXT NOT NULL
                );
                """;
}
