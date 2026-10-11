// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.Sync.Tests;

public partial record TriggerGenerationWorkflowTests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> Generate_InvalidSchema_ReturnsSpecificDatabaseErrorInlineCases()
    {
        yield return [true, "Table 'People' not found or has no columns"];
        yield return [false, "Table 'People' has no primary key"];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> FromSchema_PropagatesMetadataFailuresAndBuildsValidSchemaInlineCases()
    {
        yield return [true];
        yield return [false];
    }

    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> Create_FailureStopsBeforeExecutingSqlInlineCases()
    {
        yield return [true];
        yield return [false];
    }
}
