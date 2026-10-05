using System.Text.Json;

namespace Nimblesite.Sync.Tests;

// Implements [SYNC-EXPRESSION-TEST-CASES].
public sealed partial class LqlExpressionEvaluatorTests
{
    #region LqlExpressionEvaluator Direct Tests

    [Fact]
    public void Upper_TransformsToUppercase() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"Name":"alice"}""",
            expression: "upper(Name)",
            expected: "ALICE"
        );

    [Fact]
    public void Lower_TransformsToLowercase() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"Name":"ALICE"}""",
            expression: "lower(Name)",
            expected: "alice"
        );

    [Fact]
    public void Concat_JoinsMultipleColumns() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"FirstName":"John","LastName":"Doe"}""",
            expression: "concat(FirstName, ' ', LastName)",
            expected: "John Doe"
        );

    [Fact]
    public void Concat_WithLiterals() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"Name":"Alice"}""",
            expression: "concat('Hello, ', Name, '!')",
            expected: "Hello, Alice!"
        );

    [Fact]
    public void Substring_ExtractsPartOfString() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"Email":"alice@example.com"}""",
            expression: "substring(Email, 1, 5)",
            expected: "alice"
        );

    [Fact]
    public void Trim_RemovesWhitespace() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"Name":"  Alice  "}""",
            expression: "trim(Name)",
            expected: "Alice"
        );

    [Fact]
    public void Length_ReturnsStringLength()
    {
        var source = JsonDocument.Parse("""{"Name":"Alice"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("length(Name)", source);

        Assert.Equal(5, result);
    }

    [Fact]
    public void Coalesce_ReturnsFirstNonEmpty() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"Nickname":"","FullName":"Alice Smith"}""",
            expression: "coalesce(Nickname, FullName)",
            expected: "Alice Smith"
        );

    [Fact]
    public void Left_ExtractsLeftPart() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"Code":"ABC123XYZ"}""",
            expression: "left(Code, 3)",
            expected: "ABC"
        );

    [Fact]
    public void Right_ExtractsRightPart() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"Code":"ABC123XYZ"}""",
            expression: "right(Code, 3)",
            expected: "XYZ"
        );

    [Fact]
    public void Replace_SubstitutesText() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"Text":"Hello World"}""",
            expression: "replace(Text, 'World', 'Universe')",
            expected: "Hello Universe"
        );

    [Fact]
    public void DateFormat_FormatsDate() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"CreatedAt":"2024-06-15T10:30:00Z"}""",
            expression: "dateFormat(CreatedAt, 'yyyy-MM-dd')",
            expected: "2024-06-15"
        );

    [Fact]
    public void Pipe_ChainsFunctions() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"Name":"  alice  "}""",
            expression: "Name |> trim() |> upper()",
            expected: "ALICE"
        );

    [Fact]
    public void SimpleColumnReference_ReturnsValue() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"Name":"Alice","Age":30}""",
            expression: "Name",
            expected: "Alice"
        );

    [Fact]
    public void CaseInsensitiveColumnMatch() =>
        LqlExpressionAssertions.EvaluatesTo(
            json: """{"FirstName":"Alice"}""",
            expression: "upper(firstname)",
            expected: "ALICE"
        );

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
