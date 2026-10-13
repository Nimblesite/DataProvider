// Implements [CI-TEST-SHARED-CASE-MATRIX].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SqliteDeclarativeTriggerTests
{
    /// <summary>Independent named inputs for the shared scenario runner.</summary>
    public static System.Collections.Generic.IEnumerable<object?[]> IndependentCommentScenarioData5()
    {
        {
            yield return
            [
                "Sqlite_TriggerGuard_OldReferenceOnInsertEvent_FailsLoudly",
                "[Insert]",
                "old.Role = 'owner'",
                "fires on Insert",
            ];
        }
        {
            yield return
            [
                "Sqlite_TriggerGuard_ReservedTokenInPredicate_FailsLoudly",
                "[Delete]",
                "old.Role = '__TRG_OLD_Role__'",
                "reserved token",
            ];
        }
    }
}
