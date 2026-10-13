// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Tests;

public partial class CodeGenerationE2ETests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> GenerateQueryMethod_PostgresNullParameter_UsesMatchingColumnTypeInlineCases()
    {
        yield return ["int", "Integer"];
        yield return ["int?", "Integer"];
        yield return ["long", "Bigint"];
        yield return ["short", "Smallint"];
        yield return ["bool", "Boolean"];
        yield return ["float", "Real"];
        yield return ["double", "Double"];
        yield return ["decimal", "Numeric"];
        yield return ["DateTime", "Timestamp"];
        yield return ["Guid", "Uuid"];
        yield return ["Guid?", "Uuid"];
        yield return ["byte[]", "Bytea"];
        yield return ["string", "Text"];
    }
}
