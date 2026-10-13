// Implements [CI-TEST-SHARED-CASE-MATRIX].
// Implements [MIG-TEST-SCHEMA-FIXTURES].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial record MigrationPlatformRegressionTests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> Runner_FailedOperationReturnsErrorAndHonorsContinueOnErrorInlineCases()
    {
        yield return ["sqlite", false];
        yield return ["postgres", false];
        yield return ["sqlserver", false];
        yield return ["sqlite", true];
        yield return ["postgres", true];
        yield return ["sqlserver", true];
    }
}
