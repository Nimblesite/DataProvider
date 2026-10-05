using System.Text.Json;

namespace Nimblesite.Sync.Tests;

// Implements [SYNC-EXPRESSION-TEST-CASES].
public sealed partial class LqlMappingCornerCaseTests
{
    #region Null and Empty Value Handling

    [Fact]
    public void LqlTransform_NullStringColumn_ReturnsEmptyString()
    {
        var source = JsonDocument.Parse("""{"Name":null}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("upper(Name)", source);

        Assert.True(string.IsNullOrEmpty(result?.ToString()));
    }

    [Fact]
    public void LqlTransform_EmptyString_ReturnsEmpty() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"Name":""}""",
            expression: "upper(Name)",
            expected: ""
        );

    [Fact]
    public void Concat_WithNullColumn_ContinuesWithOtherValues()
    {
        var source = JsonDocument
            .Parse("""{"First":"John","Middle":null,"Last":"Doe"}""")
            .RootElement;

        var result = LqlExpressionEvaluator.Evaluate(
            "concat(First, ' ', Middle, ' ', Last)",
            source
        );

        // Should produce "John  Doe" (double space where null was)
        Assert.Contains("John", result?.ToString());
        Assert.Contains("Doe", result?.ToString());
    }

    [Fact]
    public void Coalesce_AllNull_ReturnsEmpty()
    {
        var source = JsonDocument.Parse("""{"A":null,"B":null,"C":""}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("coalesce(A, B, C)", source);

        // Should return empty or first non-null
        Assert.True(result == null || string.IsNullOrEmpty(result.ToString()));
    }

    [Fact]
    public void Coalesce_FirstNonNull_Returned() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"A":"","B":"","C":"found"}""",
            expression: "coalesce(A, B, C)",
            expected: "found"
        );

    #endregion

    #region Special Characters and Unicode

    [Fact]
    public void LqlTransform_UnicodeCharacters_Preserved() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"Name":"日本語テスト"}""",
            expression: "Name",
            expected: "日本語テスト"
        );

    [Fact]
    public void Upper_UnicodeString_HandledCorrectly() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"Name":"café"}""",
            expression: "upper(Name)",
            expected: "CAFÉ"
        );

    [Fact]
    public void Lower_UnicodeString_HandledCorrectly() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"Name":"MÜNCHEN"}""",
            expression: "lower(Name)",
            expected: "münchen"
        );

    [Fact]
    public void Concat_WithEmoji_Preserved() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"Prefix":"Hello","Suffix":"🎉"}""",
            expression: "concat(Prefix, ' ', Suffix)",
            expected: "Hello 🎉"
        );

    [Fact]
    public void LqlTransform_StringWithQuotes_Handled() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"Text":"Say \"Hello\" to me"}""",
            expression: "Text",
            expected: "Say \"Hello\" to me"
        );

    [Fact]
    public void LqlTransform_StringWithBackslash_Preserved() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"Path":"C:\\Users\\Test"}""",
            expression: "Path",
            expected: "C:\\Users\\Test"
        );

    [Fact]
    public void LqlTransform_StringWithNewlines_Preserved()
    {
        var source = JsonDocument.Parse("""{"Text":"Line1\nLine2\nLine3"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("Text", source);

        Assert.Contains("\n", result?.ToString());
    }

    #endregion

    #region Numeric Edge Cases

    [Fact]
    public void LqlTransform_IntegerZero_ReturnsZero()
    {
        var source = JsonDocument.Parse("""{"Value":0}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("Value", source);

        Assert.Equal(0L, result);
    }

    [Fact]
    public void LqlTransform_NegativeNumber_Preserved()
    {
        var source = JsonDocument.Parse("""{"Value":-123}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("Value", source);

        Assert.Equal(-123L, result);
    }

    [Fact]
    public void LqlTransform_LargeInteger_Preserved()
    {
        var source = JsonDocument.Parse("""{"Value":9223372036854775807}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("Value", source);

        Assert.Equal(9223372036854775807L, result);
    }

    [Fact]
    public void LqlTransform_FloatingPoint_Preserved()
    {
        var source = JsonDocument.Parse("""{"Value":3.14159265359}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("Value", source);

        Assert.Equal(3.14159265359, result);
    }

    [Fact]
    public void LqlTransform_ScientificNotation_Parsed()
    {
        var source = JsonDocument.Parse("""{"Value":1.5e10}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("Value", source);

        Assert.Equal(1.5e10, result);
    }

    #endregion

    #region Boolean Edge Cases

    [Fact]
    public void LqlTransform_BooleanTrue_Preserved()
    {
        var source = JsonDocument.Parse("""{"Active":true}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("Active", source);

        Assert.Equal(true, result);
    }

    [Fact]
    public void LqlTransform_BooleanFalse_Preserved()
    {
        var source = JsonDocument.Parse("""{"Active":false}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("Active", source);

        Assert.Equal(false, result);
    }

    #endregion

    #region String Function Edge Cases

    [Fact]
    public void Substring_StartBeyondLength_ReturnsEmpty() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"Text":"Hello"}""",
            expression: "substring(Text, 100, 5)",
            expected: ""
        );

    [Fact]
    public void Substring_LengthBeyondEnd_ReturnsTruncated() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"Text":"Hello"}""",
            expression: "substring(Text, 3, 100)",
            expected: "llo"
        );

    [Fact]
    public void Left_LengthExceedsString_ReturnsFullString() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"Text":"Hi"}""",
            expression: "left(Text, 100)",
            expected: "Hi"
        );

    [Fact]
    public void Right_LengthExceedsString_ReturnsFullString() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"Text":"Hi"}""",
            expression: "right(Text, 100)",
            expected: "Hi"
        );

    [Fact]
    public void Left_ZeroLength_ReturnsEmpty() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"Text":"Hello"}""",
            expression: "left(Text, 0)",
            expected: ""
        );

    [Fact]
    public void Replace_PatternNotFound_ReturnsOriginal() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"Text":"Hello World"}""",
            expression: "replace(Text, 'NOTFOUND', 'X')",
            expected: "Hello World"
        );

    [Fact]
    public void Replace_MultipleOccurrences_ReplacesAll() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"Text":"a-b-c-d"}""",
            expression: "replace(Text, '-', '_')",
            expected: "a_b_c_d"
        );

    [Fact]
    public void Trim_NoWhitespace_ReturnsOriginal() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"Text":"NoSpaces"}""",
            expression: "trim(Text)",
            expected: "NoSpaces"
        );

    [Fact]
    public void Trim_OnlyWhitespace_ReturnsEmpty() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"Text":"   "}""",
            expression: "trim(Text)",
            expected: ""
        );

    [Fact]
    public void Length_EmptyString_ReturnsZero()
    {
        var source = JsonDocument.Parse("""{"Text":""}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("length(Text)", source);

        Assert.Equal(0, result);
    }

    [Fact]
    public void Length_UnicodeString_CountsCodePoints()
    {
        var source = JsonDocument.Parse("""{"Text":"日本語"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("length(Text)", source);

        Assert.Equal(3, result);
    }

    #endregion

    #region Date Transform Edge Cases

    [Fact]
    public void DateFormat_InvalidDate_ReturnsOriginal() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"Date":"not-a-date"}""",
            expression: "dateFormat(Date, 'yyyy-MM-dd')",
            expected: "not-a-date"
        );

    [Fact]
    public void DateFormat_EmptyDate_ReturnsEmpty() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"Date":""}""",
            expression: "dateFormat(Date, 'yyyy-MM-dd')",
            expected: ""
        );

    [Fact]
    public void DateFormat_IsoFormat_Parsed() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"Date":"2024-12-25T15:30:00Z"}""",
            expression: "dateFormat(Date, 'yyyy-MM-dd')",
            expected: "2024-12-25"
        );

    [Fact]
    public void DateFormat_TimeOnly_Extracted() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"Date":"2024-06-15T14:30:45Z"}""",
            expression: "dateFormat(Date, 'HH:mm:ss')",
            expected: "14:30:45"
        );

    #endregion

    #region Pipe Syntax Edge Cases

    [Fact]
    public void Pipe_EmptyInput_HandledGracefully() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"Name":""}""",
            expression: "Name |> trim() |> upper()",
            expected: ""
        );

    [Fact]
    public void Pipe_MultipleFunctions_AllApplied() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"Text":"  HeLLo WoRLd  "}""",
            expression: "Text |> trim() |> lower()",
            expected: "hello world"
        );

    [Fact]
    public void Pipe_WithReplaceFunction_Works() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"Path":"users/admin/home"}""",
            expression: "Path |> replace('/', '_') |> upper()",
            expected: "USERS_ADMIN_HOME"
        );

    #endregion
}
