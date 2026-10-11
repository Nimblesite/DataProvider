// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class RlsLqlExhaustiveTests
{
    /// <summary>Independent named scenario inputs and expectations.</summary>
    public static IEnumerable<object?[]> NotOperator_AppliesToFnCall_ContextCasesData()
    {
        {
            yield return
            [
                "NotOperator_AppliesToFnCall",
                "not is_member(app_user_id(), app_tenant_id())",
                "not",
                StringComparison.OrdinalIgnoreCase,
                "is_member(",
                StringComparison.Ordinal,
            ];
        }
        {
            yield return
            [
                "IsNull_PassesThrough",
                "deleted_at is null",
                "\"deleted_at\"",
                StringComparison.Ordinal,
                "is null",
                StringComparison.OrdinalIgnoreCase,
            ];
        }
        {
            yield return
            [
                "IsNotNull_PassesThrough",
                "deleted_at is not null",
                "\"deleted_at\"",
                StringComparison.Ordinal,
                "is not null",
                StringComparison.OrdinalIgnoreCase,
            ];
        }
    }
}
