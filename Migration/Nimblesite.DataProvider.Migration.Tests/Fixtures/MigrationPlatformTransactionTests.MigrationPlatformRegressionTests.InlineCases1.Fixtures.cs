// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial record MigrationPlatformRegressionTests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> Transact_ReturnsValuePropagatesFailureAndReleasesTransactionInlineCases()
    {
        yield return ["sqlite"];
        yield return ["postgres"];
        yield return ["sqlserver"];
    }
}
