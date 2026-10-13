// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SchemaIntegrityVerifierFunctionDriftTests
{
    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Verify_WhenFunctionDefinitionDrifts_ReturnsDetailedMismatches_1() =>
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

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Verify_WhenFunctionDefinitionDrifts_ReturnsDetailedMismatches_2() =>
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
