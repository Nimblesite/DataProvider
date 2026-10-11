// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.Sync.Http.Tests;

public partial class HttpEndpointWithDatabaseTests
{
    /// <summary>Independent named scenario inputs and expectations.</summary>
    public static IEnumerable<object?[]> Update_CreatesChangelog_ContextCasesData()
    {
        {
            yield return
            [
                "Update_CreatesChangelog",
                "INSERT INTO Person (Id, Name, Email) VALUES ('p2', 'Bob', 'bob@test.com')",
                "UPDATE Person SET Name = 'Bob Updated' WHERE Id = 'p2'",
                SyncOperation.Update,
            ];
        }
        {
            yield return
            [
                "Delete_CreatesTombstone",
                "INSERT INTO Person (Id, Name, Email) VALUES ('p3', 'Charlie', 'charlie@test.com')",
                "DELETE FROM Person WHERE Id = 'p3'",
                SyncOperation.Delete,
            ];
        }
    }
}
