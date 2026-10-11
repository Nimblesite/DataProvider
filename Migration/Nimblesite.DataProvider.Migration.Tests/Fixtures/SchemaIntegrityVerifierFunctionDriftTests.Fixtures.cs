// Implements [CI-DESLOP-INDEPENDENT-DATA].
using System.Collections.Immutable;
using SchemaIntegrityOk = Outcome.Result<
    System.Collections.Immutable.ImmutableArray<string>,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Ok<
    System.Collections.Immutable.ImmutableArray<string>,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>;

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SchemaIntegrityVerifierFunctionDriftTests
{
    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Verify_WhenFunctionDefinitionDrifts_ReturnsDetailedMismatches_1(
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.PostgresFunctionDefinition>? functions,
        global::System.String schema,
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.PostgresFunctionArgumentDefinition>? arguments,
        global::System.String returns,
        global::System.String language,
        global::System.String volatility,
        global::System.Boolean securityDefiner,
        global::System.String body
    ) =>
        SchemaTestData.Schema(
            name: "live",
            functions:
            [
                SchemaTestData.PostgresFunction(
                    schema: "public",
                    name: "current_tenant_id",
                    arguments: [new PostgresFunctionArgumentDefinition { Type = "text" }],
                    returns: "text",
                    language: "plpgsql",
                    volatility: "volatile",
                    securityDefiner: false,
                    body: "select null::text"
                ),
            ]
        );

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Verify_WhenFunctionDefinitionDrifts_ReturnsDetailedMismatches_2(
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.PostgresFunctionDefinition>? functions,
        global::System.String schema,
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.PostgresFunctionArgumentDefinition>? arguments,
        global::System.String returns,
        global::System.String language,
        global::System.String volatility,
        global::System.Boolean securityDefiner,
        global::System.String body
    ) =>
        SchemaTestData.Schema(
            name: "desired",
            functions:
            [
                SchemaTestData.PostgresFunction(
                    schema: "public",
                    name: "current_tenant_id",
                    arguments: [new PostgresFunctionArgumentDefinition { Type = "text" }],
                    returns: "uuid",
                    language: "sql",
                    volatility: "stable",
                    securityDefiner: true,
                    body: "select current_setting('app.tenant_id')::uuid"
                ),
            ]
        );
}
