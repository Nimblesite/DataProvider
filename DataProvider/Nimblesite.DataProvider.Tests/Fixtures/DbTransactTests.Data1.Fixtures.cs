// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Tests;

public partial class DbTransactTests
{
    private const string Data_CreateTestTable_1 =
        @"
            CREATE TABLE IF NOT EXISTS TestTable (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL
            )";
}
