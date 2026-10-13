// Implements [CI-TEST-SHARED-CASE-MATRIX].
// Implements [MIG-TEST-SCHEMA-FIXTURES].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class RlsCurrentSettingLqlTests
{
    /// <summary>Returns the scenario inputs.</summary>
    public static IEnumerable<object?[]> Translate_CurrentSetting_NapPolicyShapes_EmitsMissingOkArgumentInlineCases()
    {
        yield return ["tenant_id = current_setting('app.tenant_id')::uuid", "'app.tenant_id'"];
        yield return ["user_id = current_setting('app.user_id')::uuid", "'app.user_id'"];
        yield return
        [
            "workspace_id = current_setting('app.workspace_id')::uuid",
            "'app.workspace_id'",
        ];
        yield return ["api_key_id = current_setting('app.api_key_id')::uuid", "'app.api_key_id'"];
        yield return ["role = current_setting('app.role')", "'app.role'"];
        yield return
        [
            "tenant_id = current_setting('request.jwt.claims.tenant_id')::uuid",
            "'request.jwt.claims.tenant_id'",
        ];
        yield return ["created_by = current_setting('app.user_id')::uuid", "'app.user_id'"];
        yield return ["updated_by = current_setting('app.user_id')::uuid", "'app.user_id'"];
        yield return ["owner_id = current_setting('app.user_id')::uuid", "'app.user_id'"];
        yield return ["actor_id = current_setting('app.user_id')::uuid", "'app.user_id'"];
        yield return
        [
            "tenant_id = current_setting('app.tenant_id')::uuid and is_member(current_setting('app.user_id')::uuid, current_setting('app.tenant_id')::uuid)",
            "'app.tenant_id'",
        ];
        yield return
        [
            "user_id = current_setting('app.user_id')::uuid or is_owner(current_setting('app.user_id')::uuid, current_setting('app.tenant_id')::uuid)",
            "'app.user_id'",
        ];
    }
}
