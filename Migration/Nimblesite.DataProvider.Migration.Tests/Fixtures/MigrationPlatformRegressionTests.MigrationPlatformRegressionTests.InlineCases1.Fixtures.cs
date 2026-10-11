// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial record MigrationPlatformRegressionTests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> AdditiveMigration_RelaxesNotNullWithoutDroppingOtherConstraintsInlineCases()
    {
        yield return ["sqlite", true, false];
        yield return ["postgres", true, false];
        yield return ["sqlserver", true, false];
        yield return ["sqlite", false, true];
        yield return ["postgres", false, true];
        yield return ["sqlserver", false, true];
        yield return ["sqlite", true, true];
        yield return ["postgres", true, true];
        yield return ["sqlserver", true, true];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> AdditiveMigration_AddsForeignKeyWhenParentOrColumnIsNewInlineCases()
    {
        yield return ["sqlite", false, false];
        yield return ["postgres", false, false];
        yield return ["sqlserver", false, false];
        yield return ["sqlite", true, false];
        yield return ["postgres", true, false];
        yield return ["sqlserver", true, false];
        yield return ["sqlite", false, true];
        yield return ["postgres", false, true];
        yield return ["sqlserver", false, true];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> AdditiveMigration_RepairsPartiallyMigratedForeignKeyInlineCases()
    {
        yield return ["sqlite"];
        yield return ["postgres"];
        yield return ["sqlserver"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> DestructiveMigration_KeepsDeclaredForeignKeysAndRemovesOnlyObsoleteOneInlineCases()
    {
        yield return ["sqlite", true, false];
        yield return ["postgres", true, false];
        yield return ["sqlserver", true, false];
        yield return ["sqlite", false, true];
        yield return ["postgres", false, true];
        yield return ["sqlserver", false, true];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> RlsMigration_ReconcilesSupportedPredicatesAndRejectsUnsupportedProviderInlineCases()
    {
        yield return ["sqlite", false, true];
        yield return ["postgres", false, true];
        yield return ["sqlserver", false, true];
        yield return ["sqlite", true, false];
        yield return ["postgres", true, false];
        yield return ["sqlserver", true, false];
        yield return ["sqlite", false, false];
        yield return ["postgres", false, false];
        yield return ["sqlserver", false, false];
    }
}
