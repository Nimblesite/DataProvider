// Implements [CI-TEST-SHARED-CASE-MATRIX].
namespace Nimblesite.DataProvider.Example.Tests;

public partial class GeneratedOperationsPlatformTests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> GeneratedCustomerOperations_PersistTwoCustomersAndOnlyUpdateTheTargetInlineCases()
    {
        yield return ["sqlite"];
        yield return ["postgres"];
        yield return ["sqlserver"];
    }
}
