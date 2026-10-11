// Implements [CI-DESLOP-INDEPENDENT-DATA].


namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SchemaDiffUniqueConstraintIssue55Tests
{
    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Calculate_AddingCompositeUniqueConstraintToExistingTable_YieldsAddOperation_1(global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition current) =>
        SchemaTestData.Schema(
            name: "desired",
            tables:
            [
                SchemaTestData.Table(
                    schema: "public",
                    name: "agent_configs",
                    columns: current.Tables[0].Columns,
                    primaryKey: new PrimaryKeyDefinition { Columns = ["id"] },
                    uniqueConstraints:
                    [
                        SchemaTestData.UniqueConstraint(
                            name: "uq_agent_configs_tenant_name",
                            columns: ["tenant_id", "name"]
                        ),
                    ]
                ),
            ]
        );
}
