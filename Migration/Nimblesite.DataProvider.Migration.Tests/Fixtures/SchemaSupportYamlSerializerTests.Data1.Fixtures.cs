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

    // Implements [RLS-PG-FUNCTION-SECURITY].
    private const string Data_FromYaml_PostgresFunctionOwnerAndSearchPath_NormalizesPinnedCatalog_1 = """
        name: nap_owner
        functions:
          - schema: public
            name: owned_with_path
            returns: boolean
            body: SELECT true
            ownerRole: nap_fn_owner
            searchPath: [pg_catalog, registry]
          - schema: public
            name: owned_default_path
            returns: boolean
            body: SELECT true
            ownerRole: nap_fn_owner
          - schema: public
            name: unmanaged
            returns: boolean
            body: SELECT true
        tables: []
        """;
}
