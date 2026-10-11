// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.Sync.Tests;

public partial class MappingConfigParserTests
{
    /// <summary>Independent named scenario inputs and expectations.</summary>
    public static IEnumerable<object?[]> Parse_DirectionPull_ParsesCorrectly_ContextCasesData()
    {
        {
            yield return
            [
                "Parse_DirectionPull_ParsesCorrectly",
                Data_Parse_DirectionPull_ParsesCorrectly_1,
                MappingDirection.Pull,
            ];
        }
        {
            yield return
            [
                "Parse_DirectionBoth_ParsesCorrectly",
                Data_Parse_DirectionBoth_ParsesCorrectly_1,
                MappingDirection.Both,
            ];
        }
    }
}
