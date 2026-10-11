// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class TriggerYamlSerializerTests
{
    private const string Data_Shared_1 = """
        name: tenant_app
        tables:
          - name: tenant_members
            columns:
              - name: id
                type: Uuid
                isNullable: false
              - name: tenant_id
                type: Uuid
                isNullable: false
              - name: role
                type: VarChar(50)
                isNullable: false
            primaryKey:
              columns:
                - id
            triggers:
              - name: assert_not_last_owner
                timing: Before
                events: [Update, Delete]
                forEachRow: true
                raiseWhen: |
                  old.role = 'owner' and not exists(
                    tenant_members
                    |> filter(fn(m) => m.tenant_id = old.tenant_id and m.role = 'owner' and m.id <> old.id)
                  )
                errorMessage: cannot remove the last owner of a tenant
        """;
}
