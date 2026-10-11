// Implements [CI-TEST-SHARED-CASE-MATRIX].
namespace Nimblesite.DataProvider.Tests;

public partial class SqlErrorTests
{
    /// <summary>Independent input and expected-value rows for the named scenarios.</summary>
    public static System.Collections.Generic.IEnumerable<object?[]> IndependentScenarioData3()
    {
        {
            yield return
            [
                "SqlError_CanBeCreatedWithMessageOnly",
                "Test error message",
                "Test error message",
            ];
        }
        {
            yield return ["SqlError_WithEmptyMessage_IsValid", "", ""];
        }
    }
}
