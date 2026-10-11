using System.Text.Json;

namespace Nimblesite.Sync.Tests;

// Implements [SYNC-EXPRESSION-TEST-CASES].
public sealed partial class LqlExpressionEvaluatorTests
{
    #region LqlExpressionEvaluator Direct Tests

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(Upper_TransformsToUppercase_CasesData))]
    public void Upper_TransformsToUppercase_Cases(
        string caseName,
        string json,
        string expression,
        string expected
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: caseName));
        LqlExpressionAssertions.EvaluatesTo(json: json, expression: expression, expected: expected);
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(Length_ReturnsStringLength_ContextCasesData))]
    public void Length_ReturnsStringLength_ContextCases(
        string caseName,
        string value0,
        string value1,
        object value2
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: caseName));
        ExerciseProjectionCaseMatrix3(value0: value0, value1: value1, value2: value2);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void LqlTransform_NullSource_ReturnsNull()
    {
        var result = LqlExpressionEvaluator.Evaluate("", JsonDocument.Parse("{}").RootElement);

        Assert.Null(result);
    }

    [Fact]
    public void LqlTransform_MissingColumn_ReturnsNull()
    {
        var source = JsonDocument.Parse("""{"Name":"Alice"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("upper(NonExistent)", source);

        // Should return empty string for missing column
        Assert.True(string.IsNullOrEmpty(result?.ToString()));
    }

    #endregion

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    private static void ExerciseProjectionCaseMatrix3(string value0, string value1, object value2)
    {
        var source = JsonDocument.Parse(value0).RootElement;

        var result = LqlExpressionEvaluator.Evaluate(value1, source);

        Assert.Equal(value2, result);
    }
}
