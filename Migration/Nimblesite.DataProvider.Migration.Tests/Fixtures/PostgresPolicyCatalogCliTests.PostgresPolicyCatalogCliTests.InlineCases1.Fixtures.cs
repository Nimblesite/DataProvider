// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresPolicyCatalogCliTests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> ColumnPredicates_CreateReapplyAndChangeWithoutFalseIntegrityDriftInlineCases()
    {
        yield return ["id = 1", "id = 2", null, null, false];
        yield return ["id = 1", "items.id = 2", "id > 0", "id > 0", true];
        yield return ["id > 0", "id > 0", "id = 1", "id = 2", false];
        yield return [null, null, "id = 1", "id = 2", false];
        yield return ["NOT (id = 1)", "NOT (id = 2)", null, null, true];
        yield return ["abs(id) = 1", "abs(id) = 2", null, null, true];
        yield return ["\"id\" = 1", "\"id\" = 2", null, null, true];
        yield return
        [
            "EXISTS (SELECT 1 FROM public.items AS other WHERE other.id = items.id)",
            "EXISTS (SELECT 1 FROM public.items AS other WHERE other.id = id)",
            null,
            null,
            true,
        ];
    }
}
