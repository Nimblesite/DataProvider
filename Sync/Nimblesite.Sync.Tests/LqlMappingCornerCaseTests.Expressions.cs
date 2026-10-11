using System.Text.Json;

namespace Nimblesite.Sync.Tests;

// Implements [SYNC-EXPRESSION-TEST-CASES].
public sealed partial class LqlMappingCornerCaseTests
{
    #region Null and Empty Value Handling

    [Fact]
    public void LqlTransform_NullStringColumn_ReturnsEmptyString()
    {
        var result = EvaluateExpressionAndRequireValue(
            value0: """{"Name":null}""",
            value1: "upper(Name)"
        );

        Assert.True(string.IsNullOrEmpty(result?.ToString()));
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(LqlTransform_EmptyString_ReturnsEmpty_CasesData))]
    public void LqlTransform_EmptyString_ReturnsEmpty_Cases(
        string caseName,
        string json,
        string expression,
        string expected
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: caseName));
        LqlExpressionAssertions.EvaluatesTo(json: json, expression: expression, expected: expected);
    }

    [Fact]
    public void Concat_WithNullColumn_ContinuesWithOtherValues()
    {
        var result = EvaluateExpressionAndRequireValue(
            value0: """{"First":"John","Middle":null,"Last":"Doe"}""",
            value1: "concat(First, ' ', Middle, ' ', Last)"
        );

        // Should produce "John  Doe" (double space where null was)
        Assert.Contains("John", result?.ToString());
        Assert.Contains("Doe", result?.ToString());
    }

    [Fact]
    public void Coalesce_AllNull_ReturnsEmpty()
    {
        var result = EvaluateExpressionAndRequireValue(
            value0: """{"A":null,"B":null,"C":""}""",
            value1: "coalesce(A, B, C)"
        );

        // Should return empty or first non-null
        Assert.True(result == null || string.IsNullOrEmpty(result.ToString()));
    }

    #endregion

    #region Special Characters and Unicode


    [Fact]
    public void LqlTransform_StringWithNewlines_Preserved()
    {
        var result = EvaluateExpressionAndRequireValue(
            value0: """{"Text":"Line1\nLine2\nLine3"}""",
            value1: "Text"
        );

        Assert.Contains("\n", result?.ToString());
    }

    #endregion

    #region Numeric Edge Cases

    [Fact]
    public void LqlTransform_IntegerZero_ReturnsZero()
    {
        var result = EvaluateExpressionAndRequireValue(value0: """{"Value":0}""", value1: "Value");

        Assert.Equal(0L, result);
    }

    [Fact]
    public void LqlTransform_NegativeNumber_Preserved()
    {
        var result = EvaluateExpressionAndRequireValue(
            value0: """{"Value":-123}""",
            value1: "Value"
        );

        Assert.Equal(-123L, result);
    }

    [Fact]
    public void LqlTransform_LargeInteger_Preserved()
    {
        var result = EvaluateExpressionAndRequireValue(
            value0: """{"Value":9223372036854775807}""",
            value1: "Value"
        );

        Assert.Equal(9223372036854775807L, result);
    }

    [Fact]
    public void LqlTransform_FloatingPoint_Preserved()
    {
        var result = EvaluateExpressionAndRequireValue(
            value0: """{"Value":3.14159265359}""",
            value1: "Value"
        );

        Assert.Equal(3.14159265359, result);
    }

    [Fact]
    public void LqlTransform_ScientificNotation_Parsed()
    {
        var result = EvaluateExpressionAndRequireValue(
            value0: """{"Value":1.5e10}""",
            value1: "Value"
        );

        Assert.Equal(1.5e10, result);
    }

    #endregion

    #region Boolean Edge Cases

    [Fact]
    public void LqlTransform_BooleanTrue_Preserved()
    {
        var result = EvaluateExpressionAndRequireValue(
            value0: """{"Active":true}""",
            value1: "Active"
        );

        Assert.Equal(true, result);
    }

    [Fact]
    public void LqlTransform_BooleanFalse_Preserved()
    {
        var result = EvaluateExpressionAndRequireValue(
            value0: """{"Active":false}""",
            value1: "Active"
        );

        Assert.Equal(false, result);
    }

    #endregion

    #region String Function Edge Cases


    [Fact]
    public void Length_EmptyString_ReturnsZero()
    {
        var result = EvaluateExpressionAndRequireValue(
            value0: """{"Text":""}""",
            value1: "length(Text)"
        );

        Assert.Equal(0, result);
    }

    [Fact]
    public void Length_UnicodeString_CountsCodePoints()
    {
        var result = EvaluateExpressionAndRequireValue(
            value0: """{"Text":"日本語"}""",
            value1: "length(Text)"
        );

        Assert.Equal(3, result);
    }

    #endregion

    #region Date Transform Edge Cases


    #endregion

    #region Pipe Syntax Edge Cases


    #endregion

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static global::System.Object? EvaluateExpressionAndRequireValue(
        string value0,
        string value1
    )
    {
        var source = JsonDocument.Parse(value0).RootElement;

        var result = LqlExpressionEvaluator.Evaluate(value1, source);

        return result;
    }
}
