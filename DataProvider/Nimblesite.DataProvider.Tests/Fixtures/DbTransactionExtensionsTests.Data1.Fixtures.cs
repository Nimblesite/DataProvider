// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Tests;

public partial class DbTransactionExtensionsTests
{
    private const string Data_CreateSchema_1 = """
        CREATE TABLE TestTable (
            Id INTEGER PRIMARY KEY,
            Name TEXT NOT NULL,
            Value INTEGER NOT NULL
        );
        """;
}
