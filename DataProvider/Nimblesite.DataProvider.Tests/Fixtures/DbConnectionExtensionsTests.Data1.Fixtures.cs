// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Tests;

public partial class DbConnectionExtensionsTests
{
    private const string Data_CreateSchema_1 = """
            CREATE TABLE TestTable (
                Id INTEGER PRIMARY KEY,
                Name TEXT NOT NULL,
                Value INTEGER NOT NULL
            );
            INSERT INTO TestTable (Name, Value) VALUES ('Alpha', 10);
            INSERT INTO TestTable (Name, Value) VALUES ('Beta', 20);
            INSERT INTO TestTable (Name, Value) VALUES ('Gamma', 30);
            """;
}
