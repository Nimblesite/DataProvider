// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.Sync.Integration.Tests;

public partial class HttpMappingSyncTests
{
    private const string Data_MultiTargetMapping_OneSourceToManyTargets_1 = """
                CREATE TABLE SalesOrder (
                    Id TEXT PRIMARY KEY,
                    CustomerId TEXT NOT NULL,
                    Total REAL NOT NULL,
                    CreatedAt TEXT
                );
                """;
}
