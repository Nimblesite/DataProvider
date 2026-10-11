// Implements [CI-DESLOP-INDEPENDENT-DATA].
using BodyError = Outcome.Result<
    string,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Error<string, Nimblesite.DataProvider.Migration.Core.MigrationError>;
using BodyOk = Outcome.Result<string, Nimblesite.DataProvider.Migration.Core.MigrationError>.Ok<
    string,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>;

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresFunctionBodyLqlTests
{
    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_ToYaml_FunctionBodyLql_EmitsBodyLqlNotBody_1(
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.PostgresFunctionDefinition>? functions,
        global::System.String name,
        global::System.String returns,
        global::System.String? bodyLql
    ) =>
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

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_SchemaDiff_BodyLqlEquivalentToInspectedBody_HasNoOperations_1(
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
                    name: "app_tenant_id",
                    returns: "uuid",
                    body: "SELECT current_setting('app.tenant_id', true)::uuid"
                ),
            ]
        );

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_SchemaDiff_BodyLqlChange_EmitsCreateOrReplaceFunction_1(
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
                    name: "app_tenant_id",
                    returns: "uuid",
                    body: "SELECT current_setting('app.tenant_id', true)::uuid"
                ),
            ]
        );
}
