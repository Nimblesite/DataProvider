// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.Sync.SQLite.Tests;

public partial class SqliteExtensionIntegrationTests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> ClientApi_MissingTableRetainsDatabaseErrorInlineCases()
    {
        yield return ["extension-list", "Failed to get clients:"];
        yield return ["repository-list", "Failed to get sync clients:"];
        yield return ["extension-upsert", "Failed to upsert client:"];
        yield return ["repository-upsert", "Failed to upsert sync client:"];
        yield return ["extension-delete", "Failed to delete stale clients:"];
        yield return ["repository-delete", "Failed to delete sync clients:"];
        yield return ["repository-origin", "Failed to get sync client:"];
        yield return ["repository-minimum", "Failed to get minimum sync version:"];
        yield return ["repository-delete-one", "Failed to delete sync client:"];
    }
}
