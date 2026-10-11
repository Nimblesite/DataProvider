// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SchemaSupportYamlSerializerTests
{
    private const string Data_FromYaml_PostgresSupportObjects_Deserializes_1 = """
        name: nap
        roles:
          - name: app_user
            grantTo: [postgres]
        functions:
          - schema: public
            name: is_member
            arguments:
              - name: tenant_id
                type: uuid
              - name: user_id
                type: uuid
            returns: boolean
            securityDefiner: true
            body: SELECT true
            executeRoles: [app_user, app_admin]
        grants:
          - schema: public
            target: AllTablesInSchema
            privileges: [SELECT, INSERT, UPDATE, DELETE]
            roles: [app_user, app_admin]
          - schema: auth
            target: Table
            objectName: users
            privileges: [SELECT]
            roles: [app_user]
            runAs: supabase_admin
        tables: []
        """;
}
