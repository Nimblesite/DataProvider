// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.Sync.Tests;

public partial class MappingEngineTests
{
    /// <summary>Independent named scenario inputs and expectations.</summary>
    public static IEnumerable<object?[]> FindMapping_WrongDirection_ReturnsNull_ContextCasesData()
    {
        {
            global::System.Func<global::Nimblesite.Sync.Core.TableMapping> input1 =
                Fixture_FindMapping_WrongDirection_ReturnsNull_1;
            yield return ["FindMapping_WrongDirection_ReturnsNull", input1, MappingDirection.Pull];
        }
        {
            global::System.Func<global::Nimblesite.Sync.Core.TableMapping> input1 =
                Fixture_FindMapping_UnmappedTable_ReturnsNull_1;
            yield return ["FindMapping_UnmappedTable_ReturnsNull", input1, MappingDirection.Push];
        }
    }
}
