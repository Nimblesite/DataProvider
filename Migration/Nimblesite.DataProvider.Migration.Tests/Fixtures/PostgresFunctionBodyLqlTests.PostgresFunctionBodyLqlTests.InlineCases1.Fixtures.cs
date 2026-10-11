// Implements [CI-TEST-SHARED-CASE-MATRIX].
// Implements [MIG-TEST-SCHEMA-FIXTURES].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresFunctionBodyLqlTests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> TranslatePostgresBody_NapScalarShapes_EmitsSqlBodyInlineCases()
    {
        yield return
        [
            "current_setting('app.tenant_id')::uuid",
            "SELECT current_setting('app.tenant_id', true)::uuid",
        ];
        yield return
        [
            "SELECT current_setting('app.user_id')::uuid",
            "SELECT current_setting('app.user_id', true)::uuid",
        ];
        yield return ["true", "SELECT true"];
        yield return
        [
            "is_member(current_setting('app.user_id')::uuid, current_setting('app.tenant_id')::uuid)",
            "SELECT is_member(current_setting('app.user_id', true)::uuid, current_setting('app.tenant_id', true)::uuid)",
        ];
    }
}
