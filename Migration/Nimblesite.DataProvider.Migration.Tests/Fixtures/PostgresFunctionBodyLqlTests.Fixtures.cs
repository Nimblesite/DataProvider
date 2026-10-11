// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresFunctionBodyLqlTests
{
    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_ToYaml_FunctionBodyLql_EmitsBodyLqlNotBody_1() =>
        SchemaTestData.Schema(
            name: "nap",
            functions:
            [
                SchemaTestData.PostgresFunction(
                    name: "app_tenant_id",
                    returns: "uuid",
                    bodyLql: "current_setting('app.tenant_id')::uuid"
                ),
            ]
        );

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_SchemaDiff_BodyLqlEquivalentToInspectedBody_HasNoOperations_1() =>
        SchemaTestData.Schema(
            name: "nap",
            functions:
            [
                SchemaTestData.PostgresFunction(
                    name: "app_tenant_id",
                    returns: "uuid",
                    body: "SELECT current_setting('app.tenant_id', true)::uuid"
                ),
            ]
        );

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_SchemaDiff_BodyLqlChange_EmitsCreateOrReplaceFunction_1() =>
        SchemaTestData.Schema(
            name: "nap",
            functions:
            [
                SchemaTestData.PostgresFunction(
                    name: "app_tenant_id",
                    returns: "uuid",
                    body: "SELECT current_setting('app.tenant_id', true)::uuid"
                ),
            ]
        );
}
