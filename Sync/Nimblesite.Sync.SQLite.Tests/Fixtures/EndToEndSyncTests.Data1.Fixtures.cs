// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.Sync.SQLite.Tests;

public partial class EndToEndSyncTests
{
    private const string Data_SetupSchema_1 = """
        CREATE TABLE Person (
            Id TEXT PRIMARY KEY,
            Name TEXT NOT NULL,
            Email TEXT NOT NULL
        );
        """;
}
