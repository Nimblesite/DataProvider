// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SqliteDeclarativeTriggerTests
{
    private const string Data_Shared_1 = """
        name: tenant_app
        tables:
          - name: TenantMembers
            schema: main
            columns:
              - name: Id
                type: Text
                isNullable: false
              - name: TenantId
                type: Text
                isNullable: false
              - name: Role
                type: Text
                isNullable: false
            primaryKey:
              columns:
                - Id
            triggers:
              - name: assert_not_last_owner
                timing: Before
                events: [Update, Delete]
                forEachRow: true
                raiseWhen: |
                  old.Role = 'owner' and not exists(
                    TenantMembers
                    |> filter(fn(m) => m.TenantId = old.TenantId and m.Role = 'owner' and m.Id <> old.Id)
                  )
                errorMessage: cannot remove the last owner of a tenant
        """;

    private const string Data_Shared_2 = """
        name: tenant_app
        tables:
          - name: TenantMembers
            schema: main
            columns:
              - name: Id
                type: Text
                isNullable: false
              - name: TenantId
                type: Text
                isNullable: false
              - name: Role
                type: Text
                isNullable: false
            primaryKey:
              columns:
                - Id
        """;
}
