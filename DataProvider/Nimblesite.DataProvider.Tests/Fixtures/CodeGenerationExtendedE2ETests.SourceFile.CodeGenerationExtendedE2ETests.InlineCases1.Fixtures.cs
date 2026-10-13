// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Tests;

public partial class CodeGenerationExtendedE2ETests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> DefaultConfig_SourceFile_RejectsInvalidInputInlineCases()
    {
        yield return ["", "record Test();", "", "namespaceName cannot be null or empty"];
        yield return [" ", "", "class Foo { }", "namespaceName cannot be null or empty"];
        yield return
        [
            "Generated",
            "",
            "",
            "At least one of modelCode or dataAccessCode must be provided",
        ];
        yield return
        [
            "Generated",
            " ",
            " ",
            "At least one of modelCode or dataAccessCode must be provided",
        ];
    }
}
