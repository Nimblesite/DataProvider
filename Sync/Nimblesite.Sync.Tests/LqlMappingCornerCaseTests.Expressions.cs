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

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(LqlTransform_IntegerZero_ReturnsZero_CasesData))]
    public void LqlTransform_IntegerZero_ReturnsZero_Cases(
        string caseName,
        global::System.String value0,
        global::System.String value1,
        global::System.Object value2
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: caseName));
        ExerciseTypedCaseMatrix1(value0: value0, value1: value1, value2: value2);
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

    #endregion

    #region Boolean Edge Cases


    #endregion

    #region String Function Edge Cases


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

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    private static void ExerciseTypedCaseMatrix1(
        global::System.String value0,
        global::System.String value1,
        global::System.Object value2
    )
    {
        var result = EvaluateExpressionAndRequireValue(value0: value0, value1: value1);

        Assert.Equal(value2, result);
    }
}
