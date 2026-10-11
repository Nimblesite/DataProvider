// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.Sync.Http.Tests;

public partial class HttpEndpointWithDatabaseTests
{
    private const string Data_Shared_1 = """
                CREATE TABLE Person (
                    Id TEXT PRIMARY KEY,
                    Name TEXT NOT NULL,
                    Email TEXT
                );
                """;
}
