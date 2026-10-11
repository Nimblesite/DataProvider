// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresRlsE2ETests
{
    private static global::Nimblesite.DataProvider.Migration.Core.RlsPolicySetDefinition Fixture_SchemaDiff_AddsNewPolicy_ToExistingRlsTable_1() =>
        new RlsPolicySetDefinition
        {
            Policies =
            [
                SchemaTestData.RlsPolicy(
                    name: "owner_isolation",
                    operations: [RlsOperation.All],
                    usingLql: "owner_id = current_user_id()::uuid",
                    withCheckLql: "owner_id = current_user_id()::uuid"
                ),
                SchemaTestData.RlsPolicy(
                    name: "extra_policy",
                    operations: [RlsOperation.Select],
                    usingLql: "true"
                ),
            ],
        };
}
