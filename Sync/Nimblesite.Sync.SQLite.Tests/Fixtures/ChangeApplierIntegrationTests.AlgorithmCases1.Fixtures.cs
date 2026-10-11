// Implements [CI-TEST-SHARED-CASE-MATRIX].
namespace Nimblesite.Sync.SQLite.Tests;

public partial class ChangeApplierIntegrationTests
{
    public static IEnumerable<object?[]> ApplyChange_Insert_WithNullPayload_ReturnsError_CasesData()
    {
        {
            global::System.Func<global::Nimblesite.Sync.Core.SyncLogEntry> input0 =
                Fixture_ApplyChange_Insert_WithNullPayload_ReturnsError_1;
            yield return ["ApplyChange_Insert_WithNullPayload_ReturnsError", input0];
        }
        {
            global::System.Func<global::Nimblesite.Sync.Core.SyncLogEntry> input0 =
                Fixture_ApplyChange_Insert_WithEmptyPayload_ReturnsError_1;
            yield return ["ApplyChange_Insert_WithEmptyPayload_ReturnsError", input0];
        }
        {
            global::System.Func<global::Nimblesite.Sync.Core.SyncLogEntry> input0 =
                Fixture_ApplyChange_Delete_InvalidPkValue_ReturnsError_1;
            yield return ["ApplyChange_Delete_InvalidPkValue_ReturnsError", input0];
        }
    }
}
