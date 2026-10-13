// Implements [CI-TEST-SHARED-CASE-MATRIX].
namespace Nimblesite.Sync.Tests;

public partial class LqlMappingCornerCaseTests
{
    public static IEnumerable<object?[]> LqlTransform_IntegerZero_ReturnsZero_CasesData()
    {
        {
            yield return ["LqlTransform_IntegerZero_ReturnsZero", """{"Value":0}""", "Value", 0L];
        }
        {
            yield return
            [
                "LqlTransform_LargeInteger_Preserved",
                """{"Value":9223372036854775807}""",
                "Value",
                9223372036854775807L,
            ];
        }
        {
            yield return
            [
                "LqlTransform_FloatingPoint_Preserved",
                """{"Value":3.14159265359}""",
                "Value",
                3.14159265359,
            ];
        }
        {
            yield return
            [
                "LqlTransform_ScientificNotation_Parsed",
                """{"Value":1.5e10}""",
                "Value",
                1.5e10,
            ];
        }
        {
            yield return
            [
                "LqlTransform_BooleanTrue_Preserved",
                """{"Active":true}""",
                "Active",
                true,
            ];
        }
        {
            yield return
            [
                "LqlTransform_BooleanFalse_Preserved",
                """{"Active":false}""",
                "Active",
                false,
            ];
        }
        {
            yield return ["Length_EmptyString_ReturnsZero", """{"Text":""}""", "length(Text)", 0];
        }
        {
            yield return
            [
                "Length_UnicodeString_CountsCodePoints",
                """{"Text":"日本語"}""",
                "length(Text)",
                3,
            ];
        }
    }
}
