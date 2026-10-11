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

    [Fact]
    public void Length_ReturnsStringLength()
    {
        var source = JsonDocument.Parse("""{"Name":"Alice"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("length(Name)", source);

        Assert.Equal(5, result);
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

    [Fact]
    public void LqlTransform_NumericColumn_Works()
    {
        var source = JsonDocument.Parse("""{"Price":99.99}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("Price", source);

        Assert.Equal(99.99, result);
    }

    [Fact]
    public void LqlTransform_BooleanColumn_Works()
    {
        var source = JsonDocument.Parse("""{"IsActive":true}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("IsActive", source);

        Assert.Equal(true, result);
    }

    #endregion
}
