// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SchemaDiffUniqueConstraintIssue55Tests
{
    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Calculate_AddingCompositeUniqueConstraintToExistingTable_YieldsAddOperation_1(
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.TableDefinition>? tables,
        global::System.String schema,
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.ColumnDefinition>? columns,
        global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition current,
        global::Nimblesite.DataProvider.Migration.Core.PrimaryKeyDefinition? primaryKey,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.UniqueConstraintDefinition>? uniqueConstraints,
        global::System.String? name,
        global::System.Collections.Generic.IReadOnlyList<global::System.String>? columns
    ) =>
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
