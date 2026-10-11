// Implements [CI-DESLOP-INDEPENDENT-DATA].


namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresUniqueConstraintIssue55E2ETests
{
    private static global::Nimblesite.DataProvider.Migration.Core.TableDefinition Fixture_Schema_1() =>
        SchemaTestData.Table(
            schema: SchemaName,
            name: "tenants",
            columns:
            [
                SchemaTestData.Column(name: "id", type: PortableTypes.Uuid, isNullable: false),
            ],
            primaryKey: new PrimaryKeyDefinition { Columns = ["id"] }
        );

    private static global::Nimblesite.DataProvider.Migration.Core.TableDefinition Fixture_Schema_2(global::System.Boolean includeUnique) =>
        SchemaTestData.Table(
            schema: SchemaName,
            name: "agent_configs",
            columns:
            [
                SchemaTestData.Column(name: "id", type: PortableTypes.Uuid, isNullable: false),
                SchemaTestData.Column(
                    name: "tenant_id",
                    type: PortableTypes.Uuid,
                    isNullable: false
                ),
                SchemaTestData.Column(name: "name", type: PortableTypes.Text, isNullable: false),
            ],
            primaryKey: new PrimaryKeyDefinition { Columns = ["id"] },
            foreignKeys:
            [
                SchemaTestData.ForeignKey(
                    columns: ["tenant_id"],
                    referencedSchema: SchemaName,
                    referencedTable: "tenants",
                    referencedColumns: ["id"],
                    onDelete: ForeignKeyAction.Cascade
                ),
            ],
            uniqueConstraints: includeUnique
                ?
                [
                    SchemaTestData.UniqueConstraint(
                        name: "uq_agent_configs_tenant_name",
                        columns: ["tenant_id", "name"]
                    ),
                ]
                : []
        );
}
