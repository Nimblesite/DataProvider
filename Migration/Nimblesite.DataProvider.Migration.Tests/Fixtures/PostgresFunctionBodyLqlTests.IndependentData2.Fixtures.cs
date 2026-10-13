// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresFunctionBodyLqlTests
{
    private static global::Nimblesite.DataProvider.Migration.Core.CreateOrReplaceFunctionOperation Dataset_Generate_CreateFunction_BodyLqlCurrentSetting_EmitsDollarQuotedSqlBody_1() =>
        new CreateOrReplaceFunctionOperation(
            SchemaTestData.PostgresFunction(
                name: "app_tenant_id",
                returns: "uuid",
                bodyLql: "current_setting('app.tenant_id')::uuid"
            )
        );

    private static global::Nimblesite.DataProvider.Migration.Core.CreateOrReplaceFunctionOperation Dataset_Generate_CreateFunction_BodyLqlExists_EmitsSelectExistsBody_1() =>
        new CreateOrReplaceFunctionOperation(
            SchemaTestData.PostgresFunction(
                name: "is_member",
                returns: "boolean",
                arguments:
                [
                    SchemaTestData.PostgresFunctionArgument(name: "u", type: "uuid"),
                    SchemaTestData.PostgresFunctionArgument(name: "t", type: "uuid"),
                ],
                securityDefiner: true,
                bodyLql: "exists(tenant_members |> filter(fn(m) => m.user_id = u and m.tenant_id = t))"
            )
        );

    private static global::Nimblesite.DataProvider.Migration.Core.CreateOrReplaceFunctionOperation Dataset_Generate_CreateFunction_BodyAndBodyLql_Throws_1() =>
        new CreateOrReplaceFunctionOperation(
            SchemaTestData.PostgresFunction(name: "bad", body: "SELECT true", bodyLql: "true")
        );

    private static global::Nimblesite.DataProvider.Migration.Core.PostgresFunctionDefinition Dataset_SchemaDiff_BodyLqlEquivalentToInspectedBody_HasNoOperations_1() =>
        SchemaTestData.PostgresFunction(
            name: "app_tenant_id",
            returns: "uuid",
            bodyLql: "current_setting('app.tenant_id')::uuid"
        );

    private static global::Nimblesite.DataProvider.Migration.Core.PostgresFunctionDefinition Dataset_SchemaDiff_BodyLqlChange_EmitsCreateOrReplaceFunction_1() =>
        SchemaTestData.PostgresFunction(
            name: "app_tenant_id",
            returns: "text",
            bodyLql: "current_setting('app.tenant_id')"
        );
}
