// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SchemaSupportYamlSerializerTests
{
    // Implements [RLS-PG-FUNCTION-SECURITY].
    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_ToYaml_PostgresFunctionOwner_EmitsOwnerAndExplicitSchemasOnly_1() =>
        SchemaTestData.Schema(
            name: "nap_owner_yaml",
            functions:
            [
                SchemaTestData.PostgresFunction(
                    name: "is_member",
                    returns: "boolean",
                    body: "SELECT true",
                    ownerRole: "nap_fn_owner",
                    searchPath: ["pg_catalog", "registry"]
                ),
            ]
        );

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_ToYaml_PostgresSupportObjects_OmitsSemanticDefaults_1() =>
        SchemaTestData.Schema(
            name: "nap",
            functions:
            [
                SchemaTestData.PostgresFunction(
                    name: "app_user_id",
                    returns: "uuid",
                    body: "SELECT NULL::uuid"
                ),
            ]
        );

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_ToYaml_PostgresGrantRunAs_EmitsRunAs_1() =>
        SchemaTestData.Schema(
            name: "nap",
            grants:
            [
                SchemaTestData.PostgresGrant(
                    schema: "auth",
                    target: PostgresGrantTarget.Schema,
                    privileges: ["USAGE"],
                    roles: ["app_user", "app_admin"],
                    runAs: "supabase_admin"
                ),
            ]
        );
}
