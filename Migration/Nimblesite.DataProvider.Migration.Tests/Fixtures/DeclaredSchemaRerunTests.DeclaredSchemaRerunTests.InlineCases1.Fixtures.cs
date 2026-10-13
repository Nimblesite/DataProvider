// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial record DeclaredSchemaRerunTests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> Migrate_DeclaredSchema_VerifiesAndRerunsCleanlyInlineCases()
    {
        yield return ["sqlite", PrimaryKeyDefaultNullability];
        yield return ["postgres", PrimaryKeyDefaultNullability];
        yield return ["sqlserver", PrimaryKeyDefaultNullability];
        yield return ["sqlite", ParenthesizedDefault];
        yield return ["postgres", ParenthesizedDefault];
        yield return ["sqlserver", ParenthesizedDefault];
    }
}
