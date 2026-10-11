// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.Sync.Tests;

public partial class SubscriptionManagerTests
{
    /// <summary>Independent named scenario inputs and expectations.</summary>
    public static IEnumerable<object?[]> MatchesChange_RecordSubscription_NullFilter_ReturnsFalse_ContextCasesData()
    {
        {
            global::System.Func<global::Nimblesite.Sync.Core.SyncSubscription> input0 =
                Dataset_MatchesChange_RecordSubscription_NullFilter_ReturnsFalse_1;
            yield return ["MatchesChange_RecordSubscription_NullFilter_ReturnsFalse", input0];
        }
        {
            global::System.Func<global::Nimblesite.Sync.Core.SyncSubscription> input0 =
                Dataset_MatchesChange_RecordSubscription_EmptyFilter_ReturnsFalse_1;
            yield return ["MatchesChange_RecordSubscription_EmptyFilter_ReturnsFalse", input0];
        }
    }
}
