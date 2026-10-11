// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SqliteDeclarativeTriggerTests
{
    private static string SqlData_TryApplyGuardVariant_1(
        global::System.String events,
        global::System.String raiseWhen,
        global::System.String? extra
    ) =>
        $"""
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
                  - name: guard_variant
                    events: {events}
                    raiseWhen: "{raiseWhen}"{extra}
            """;
}
