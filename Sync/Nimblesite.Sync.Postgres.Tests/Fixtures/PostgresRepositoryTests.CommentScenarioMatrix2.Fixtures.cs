// Implements [CI-TEST-SHARED-CASE-MATRIX].
namespace Nimblesite.Sync.Postgres.Tests;

public partial class PostgresRepositoryTests
{
    /// <summary>Independent named inputs for the shared scenario runner.</summary>
    public static System.Collections.Generic.IEnumerable<object?[]> IndependentCommentScenarioData2()
    {
        {
            yield return
            [
                "ApplyChange_Update_UpdatesRow",
                Data_ApplyChange_Update_UpdatesRow_1,
                "test_update",
                "{\"id\":\"u1\"}",
                "{\"id\":\"u1\",\"name\":\"Updated\"}",
                "SELECT name FROM test_update WHERE id = 'u1'",
                "Updated",
            ];
        }
        {
            yield return
            [
                "ApplyChange_Update_NonExistent_UpsertsViaInsert",
                Data_ApplyChange_Update_NonExistent_UpsertsViaInsert_1,
                "test_update_upsert",
                "{\"id\":\"new1\"}",
                "{\"id\":\"new1\",\"name\":\"NewRecord\"}",
                "SELECT name FROM test_update_upsert WHERE id = 'new1'",
                "NewRecord",
            ];
        }
    }
}
