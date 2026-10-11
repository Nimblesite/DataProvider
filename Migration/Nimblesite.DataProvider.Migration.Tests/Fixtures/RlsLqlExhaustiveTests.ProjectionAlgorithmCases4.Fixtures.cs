// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class RlsLqlExhaustiveTests
{
    /// <summary>Independent named scenario inputs and expectations.</summary>
    public static IEnumerable<object?[]> CustomGucReader_AppTenantId_PassesThrough_Postgres_ContextCasesData()
    {
        {
            yield return
            [
                "CustomGucReader_AppTenantId_PassesThrough_Postgres",
                "tenant_id = app_tenant_id()",
                "\"tenant_id\"",
                "app_tenant_id()",
                "\"app_tenant_id\"",
            ];
        }
        {
            yield return
            [
                "TypeCast_ColonColon_TypeNameNotQuoted",
                "owner_id::uuid = current_user_id()::uuid",
                "\"owner_id\"",
                "::uuid",
                "\"uuid\"",
            ];
        }
    }
}
