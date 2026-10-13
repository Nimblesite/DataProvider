// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.Lql.Tests;

public partial class LqlErrorHandlingTests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> UnderscorePipelineBase_ShouldParseAsTableNameInlineCases()
    {
        yield return ["tenant_members"];
        yield return ["fhir_patient"];
    }
}
