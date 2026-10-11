// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SchemaSupportYamlSerializerTests
{
    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_ToYaml_PostgresSupportObjects_OmitsSemanticDefaults_1(
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.PostgresFunctionDefinition>? functions,
        global::System.String name,
        global::System.String returns,
        global::System.String body
    ) =>
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

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_ToYaml_PostgresGrantRunAs_EmitsRunAs_1(
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.PostgresGrantDefinition>? grants,
        global::System.String schema,
        global::Nimblesite.DataProvider.Migration.Core.PostgresGrantTarget target,
        global::System.Collections.Generic.IReadOnlyList<global::System.String>? privileges,
        global::System.Collections.Generic.IReadOnlyList<global::System.String>? roles,
        global::System.String? runAs
    ) =>
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
