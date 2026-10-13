// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresFunctionBodyLqlTests
{
    private const string Data_TranslatePostgresBody_ExistsPipeline_EmitsSelectExists_1 = """
        exists(
          tenant_members
          |> filter(fn(m) => m.user_id = u and m.tenant_id = t)
        )
        """;

    private const string Data_TranslatePostgresBody_ExistsPipelineWithInList_EmitsInPredicate_1 = """
        exists(
          tenant_members
          |> filter(fn(m) => m.user_id = u and m.tenant_id = t and m.role in ('owner', 'admin'))
        )
        """;

    private const string Data_FromYaml_FunctionBodyLql_Deserializes_1 = """
        name: nap
        functions:
          - schema: public
            name: app_tenant_id
            returns: uuid
            bodyLql: current_setting('app.tenant_id')::uuid
        tables: []
        """;

    private const string Data_FromYaml_FunctionBodyAndBodyLql_Throws_1 = """
        name: nap
        functions:
          - name: app_tenant_id
            returns: uuid
            body: SELECT NULL::uuid
            bodyLql: current_setting('app.tenant_id')::uuid
        tables: []
        """;
}
