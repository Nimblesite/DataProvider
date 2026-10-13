// Implements [CI-TEST-SHARED-CASE-MATRIX].
namespace Nimblesite.Sync.Tests;

public partial class LqlMappingCornerCaseTests
{
    public static IEnumerable<object?[]> LqlTransform_EmptyString_ReturnsEmpty_CasesData()
    {
        yield return
        [
            "LqlTransform_EmptyString_ReturnsEmpty",
            """{"Name":""}""",
            "upper(Name)",
            "",
        ];
        yield return
        [
            "Coalesce_FirstNonNull_Returned",
            """{"A":"","B":"","C":"found"}""",
            "coalesce(A, B, C)",
            "found",
        ];
        yield return
        [
            "LqlTransform_UnicodeCharacters_Preserved",
            """{"Name":"日本語テスト"}""",
            "Name",
            "日本語テスト",
        ];
        yield return
        [
            "Upper_UnicodeString_HandledCorrectly",
            """{"Name":"café"}""",
            "upper(Name)",
            "CAFÉ",
        ];
        yield return
        [
            "Lower_UnicodeString_HandledCorrectly",
            """{"Name":"MÜNCHEN"}""",
            "lower(Name)",
            "münchen",
        ];
        yield return
        [
            "Concat_WithEmoji_Preserved",
            """{"Prefix":"Hello","Suffix":"🎉"}""",
            "concat(Prefix, ' ', Suffix)",
            "Hello 🎉",
        ];
        yield return
        [
            "LqlTransform_StringWithQuotes_Handled",
            """{"Text":"Say \"Hello\" to me"}""",
            "Text",
            "Say \"Hello\" to me",
        ];
        yield return
        [
            "LqlTransform_StringWithBackslash_Preserved",
            """{"Path":"C:\\Users\\Test"}""",
            "Path",
            "C:\\Users\\Test",
        ];
        yield return
        [
            "Substring_StartBeyondLength_ReturnsEmpty",
            """{"Text":"Hello"}""",
            "substring(Text, 100, 5)",
            "",
        ];
        yield return
        [
            "Substring_LengthBeyondEnd_ReturnsTruncated",
            """{"Text":"Hello"}""",
            "substring(Text, 3, 100)",
            "llo",
        ];
        yield return
        [
            "Left_LengthExceedsString_ReturnsFullString",
            """{"Text":"Hi"}""",
            "left(Text, 100)",
            "Hi",
        ];
        yield return
        [
            "Right_LengthExceedsString_ReturnsFullString",
            """{"Text":"Hi"}""",
            "right(Text, 100)",
            "Hi",
        ];
        yield return ["Left_ZeroLength_ReturnsEmpty", """{"Text":"Hello"}""", "left(Text, 0)", ""];
        yield return
        [
            "Replace_PatternNotFound_ReturnsOriginal",
            """{"Text":"Hello World"}""",
            "replace(Text, 'NOTFOUND', 'X')",
            "Hello World",
        ];
        yield return
        [
            "Replace_MultipleOccurrences_ReplacesAll",
            """{"Text":"a-b-c-d"}""",
            "replace(Text, '-', '_')",
            "a_b_c_d",
        ];
        yield return
        [
            "Trim_NoWhitespace_ReturnsOriginal",
            """{"Text":"NoSpaces"}""",
            "trim(Text)",
            "NoSpaces",
        ];
        yield return ["Trim_OnlyWhitespace_ReturnsEmpty", """{"Text":"   "}""", "trim(Text)", ""];
        yield return
        [
            "DateFormat_InvalidDate_ReturnsOriginal",
            """{"Date":"not-a-date"}""",
            "dateFormat(Date, 'yyyy-MM-dd')",
            "not-a-date",
        ];
        yield return
        [
            "DateFormat_EmptyDate_ReturnsEmpty",
            """{"Date":""}""",
            "dateFormat(Date, 'yyyy-MM-dd')",
            "",
        ];
        yield return
        [
            "DateFormat_IsoFormat_Parsed",
            """{"Date":"2024-12-25T15:30:00Z"}""",
            "dateFormat(Date, 'yyyy-MM-dd')",
            "2024-12-25",
        ];
        yield return
        [
            "DateFormat_TimeOnly_Extracted",
            """{"Date":"2024-06-15T14:30:45Z"}""",
            "dateFormat(Date, 'HH:mm:ss')",
            "14:30:45",
        ];
        yield return
        [
            "Pipe_EmptyInput_HandledGracefully",
            """{"Name":""}""",
            "Name |> trim() |> upper()",
            "",
        ];
        yield return
        [
            "Pipe_MultipleFunctions_AllApplied",
            """{"Text":"  HeLLo WoRLd  "}""",
            "Text |> trim() |> lower()",
            "hello world",
        ];
        yield return
        [
            "Pipe_WithReplaceFunction_Works",
            """{"Path":"users/admin/home"}""",
            "Path |> replace('/', '_') |> upper()",
            "USERS_ADMIN_HOME",
        ];
    }
}
