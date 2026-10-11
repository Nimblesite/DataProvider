// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SchemaDiffUniqueConstraintIssue55Tests
{
    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Dataset_Calculate_AddingCompositeUniqueConstraintToExistingTable_YieldsAddOperation_1(
        global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition current
    ) =>
        Fixture_Calculate_AddingCompositeUniqueConstraintToExistingTable_YieldsAddOperation_1(
            current: current
        );

    private static global::Nimblesite.DataProvider.Migration.Core.UniqueConstraintDefinition Dataset_Calculate_ReplayAfterUniqueConstraintApplied_YieldsNoUniqueOperation_1() =>
        SchemaTestData.UniqueConstraint(
            name: "uq_agent_configs_tenant_name",
            columns: ["tenant_id", "name"]
        );
}
