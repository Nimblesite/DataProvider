// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SqliteRlsMigrationTests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> Sqlite_SelectPolicy_ChangedUsingPredicateBlocksPreviouslyVisibleRowsInlineCases()
    {
        yield return [false];
        yield return [true];
    }
}
