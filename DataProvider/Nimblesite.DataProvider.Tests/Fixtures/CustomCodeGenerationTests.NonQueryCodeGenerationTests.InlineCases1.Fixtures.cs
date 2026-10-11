// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Tests;

public partial class NonQueryCodeGenerationTests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> GenerateNonQueryMethod_EmptyClassName_ReturnsError_CasesInlineCases()
    {
        yield return ["", "Test", "UPDATE x SET y = 1"];
        yield return ["TestExtensions", "", "UPDATE x SET y = 1"];
        yield return ["TestExtensions", "Test", ""];
    }
}
