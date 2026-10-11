// Implements [CI-TEST-SHARED-CASE-MATRIX].
// Implements [CI-TEST-SHARED-COMMAND-PARAMETERS].
// Implements [CI-TEST-SHARED-COMMAND].

namespace Nimblesite.Sync.Http.Tests;

public partial class HttpMappingE2ETests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> LqlExpression_StringTransform_ReturnsExpectedPayloadInlineCases()
    {
        yield return
        [
            "name-concat",
            "Person",
            "person",
            "{\"Id\":\"p1\"}",
            "full_name",
            "concat(FirstName, ' ', LastName)",
            "{\"Id\":\"p1\",\"FirstName\":\"John\",\"LastName\":\"Doe\"}",
            "John Doe",
        ];
        yield return
        [
            "coalesce-test",
            "Contact",
            "contact",
            "{\"Id\":\"c1\"}",
            "phone",
            "coalesce(Mobile, Home, Office)",
            "{\"Id\":\"c1\",\"Mobile\":\"\",\"Home\":\"555-1234\",\"Office\":\"555-5678\"}",
            "555-1234",
        ];
        yield return
        [
            "substring-test",
            "Product",
            "product",
            "{\"Id\":\"p1\"}",
            "sku_prefix",
            "substring(SKU, 1, 3)",
            "{\"Id\":\"p1\",\"SKU\":\"ABC-12345\"}",
            "ABC",
        ];
        yield return
        [
            "empty-test",
            "Source",
            "target",
            "{\"Id\":\"1\"}",
            "upper_name",
            "upper(Name)",
            "{\"Id\":\"1\",\"Name\":\"\"}",
            "",
        ];
        yield return
        [
            "replace-test",
            "Source",
            "target",
            "{\"Id\":\"1\"}",
            "clean_phone",
            "replace(Phone, '-', '')",
            "{\"Id\":\"1\",\"Phone\":\"555-123-4567\"}",
            "5551234567",
        ];
        yield return
        [
            "left-test",
            "Source",
            "target",
            "{\"Id\":\"1\"}",
            "initials",
            "left(Name, 2)",
            "{\"Id\":\"1\",\"Name\":\"Alexander\"}",
            "Al",
        ];
        yield return
        [
            "right-test",
            "Source",
            "target",
            "{\"Id\":\"1\"}",
            "last_four",
            "right(CardNumber, 4)",
            "{\"Id\":\"1\",\"CardNumber\":\"1234567890123456\"}",
            "3456",
        ];
        yield return
        [
            "trim-test",
            "Source",
            "target",
            "{\"Id\":\"1\"}",
            "clean_name",
            "trim(Name)",
            "{\"Id\":\"1\",\"Name\":\"  Hello World  \"}",
            "Hello World",
        ];
        yield return
        [
            "nested-test",
            "Source",
            "target",
            "{\"Id\":\"1\"}",
            "display",
            "concat(Title, ': ', FirstName, ' ', LastName, ' (', Department, ')')",
            "{\"Id\":\"1\",\"Title\":\"Dr\",\"FirstName\":\"John\",\"LastName\":\"Smith\",\"Department\":\"Engineering\"}",
            "Dr: John Smith (Engineering)",
        ];
        yield return
        [
            "tz-test",
            "Source",
            "target",
            "{\"Id\":\"1\"}",
            "date_only",
            "CreatedAt |> dateFormat('yyyy-MM-dd')",
            "{\"Id\":\"1\",\"CreatedAt\":\"2024-12-25T23:30:00+00:00\"}",
            "2024-12-25",
        ];
        yield return
        [
            "pipeline-test",
            "Source",
            "target",
            "{\"Id\":\"1\"}",
            "clean_name",
            "Name |> trim() |> upper()",
            "{\"Id\":\"1\",\"Name\":\"  hello world  \"}",
            "HELLO WORLD",
        ];
    }
}
