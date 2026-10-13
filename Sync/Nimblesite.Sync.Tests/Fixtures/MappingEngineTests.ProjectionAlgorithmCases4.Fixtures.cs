// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.Sync.Tests;

public partial class MappingEngineTests
{
    /// <summary>Independent named scenario inputs and expectations.</summary>
    public static IEnumerable<object?[]> FindMapping_ExactMatch_ReturnsMapping_ContextCasesData()
    {
        {
            global::System.Func<global::Nimblesite.Sync.Core.TableMapping> input1 =
                Fixture_FindMapping_ExactMatch_ReturnsMapping_1;
            yield return ["FindMapping_ExactMatch_ReturnsMapping", input1, "user-to-customer"];
        }
        {
            global::System.Func<global::Nimblesite.Sync.Core.TableMapping> input1 =
                Fixture_FindMapping_BothDirection_MatchesPush_1;
            yield return ["FindMapping_BothDirection_MatchesPush", input1, "user-both"];
        }
    }
}
