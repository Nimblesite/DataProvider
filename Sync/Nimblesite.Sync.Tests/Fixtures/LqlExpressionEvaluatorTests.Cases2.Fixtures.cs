// Implements [CI-TEST-SHARED-CASE-MATRIX].
namespace Nimblesite.Sync.Tests;

public partial class LqlExpressionEvaluatorTests
{
    public static IEnumerable<object?[]> Upper_TransformsToUppercase_CasesData()
    {
        yield return
        [
            "Upper_TransformsToUppercase",
            """{"Name":"alice"}""",
            "upper(Name)",
            "ALICE",
        ];
        yield return
        [
            "Lower_TransformsToLowercase",
            """{"Name":"ALICE"}""",
            "lower(Name)",
            "alice",
        ];
        yield return
        [
            "Concat_JoinsMultipleColumns",
            """{"FirstName":"John","LastName":"Doe"}""",
            "concat(FirstName, ' ', LastName)",
            "John Doe",
        ];
        yield return
        [
            "Concat_WithLiterals",
            """{"Name":"Alice"}""",
            "concat('Hello, ', Name, '!')",
            "Hello, Alice!",
        ];
        yield return
        [
            "Substring_ExtractsPartOfString",
            """{"Email":"alice@example.com"}""",
            "substring(Email, 1, 5)",
            "alice",
        ];
        yield return ["Trim_RemovesWhitespace", """{"Name":"  Alice  "}""", "trim(Name)", "Alice"];
        yield return
        [
            "Coalesce_ReturnsFirstNonEmpty",
            """{"Nickname":"","FullName":"Alice Smith"}""",
            "coalesce(Nickname, FullName)",
            "Alice Smith",
        ];
        yield return ["Left_ExtractsLeftPart", """{"Code":"ABC123XYZ"}""", "left(Code, 3)", "ABC"];
        yield return
        [
            "Right_ExtractsRightPart",
            """{"Code":"ABC123XYZ"}""",
            "right(Code, 3)",
            "XYZ",
        ];
        yield return
        [
            "Replace_SubstitutesText",
            """{"Text":"Hello World"}""",
            "replace(Text, 'World', 'Universe')",
            "Hello Universe",
        ];
        yield return
        [
            "DateFormat_FormatsDate",
            """{"CreatedAt":"2024-06-15T10:30:00Z"}""",
            "dateFormat(CreatedAt, 'yyyy-MM-dd')",
            "2024-06-15",
        ];
        yield return
        [
            "Pipe_ChainsFunctions",
            """{"Name":"  alice  "}""",
            "Name |> trim() |> upper()",
            "ALICE",
        ];
        yield return
        [
            "SimpleColumnReference_ReturnsValue",
            """{"Name":"Alice","Age":30}""",
            "Name",
            "Alice",
        ];
        yield return
        [
            "CaseInsensitiveColumnMatch",
            """{"FirstName":"Alice"}""",
            "upper(firstname)",
            "ALICE",
        ];
    }
}
