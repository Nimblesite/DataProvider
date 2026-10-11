// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Tests;

public partial class AdoNetDatabaseEffectsTests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> Rejects_null_connection_string_CasesInlineCases()
    {
        yield return ["", "SELECT 1"];
        yield return ["Data Source=:memory:", "   "];
        yield return ["Data Source=:memory:", "SELECT * FROM table_that_does_not_exist"];
    }
}
