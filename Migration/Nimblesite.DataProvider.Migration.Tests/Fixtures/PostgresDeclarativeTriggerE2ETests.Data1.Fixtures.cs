// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresDeclarativeTriggerE2ETests
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
                events: [Update, Delete]
                raiseWhen: |
                  old.role = 'owner' and not exists(
                    tenant_members
                    |> filter(fn(m) => m.tenant_id = old.tenant_id and m.role = 'owner' and m.id <> old.id)
                  )
                errorMessage: cannot remove the last owner of a tenant
        """;

    private const string Data_Shared_2 = """
        name: tenant_app
        tables:
          - name: TenantMembers
            columns:
              - name: Id
                type: Uuid
                isNullable: false
              - name: TenantId
                type: Uuid
                isNullable: false
              - name: Role
                type: VarChar(50)
                isNullable: false
            primaryKey:
              columns:
                - Id
            triggers:
              - name: assert_not_last_owner
                events: [Update, Delete]
                raiseWhen: |
                  old.Role = 'owner' and not exists(
                    TenantMembers
                    |> filter(fn(m) => m.TenantId = old.TenantId and m.Role = 'owner' and m.Id <> old.Id)
                  )
                errorMessage: cannot remove the last owner of a tenant
        """;
}
