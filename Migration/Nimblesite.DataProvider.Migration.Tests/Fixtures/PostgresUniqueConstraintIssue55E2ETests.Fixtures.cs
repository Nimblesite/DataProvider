// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresUniqueConstraintIssue55E2ETests
{
    private static global::Nimblesite.DataProvider.Migration.Core.TableDefinition Fixture_Schema_1(
        global::System.String schema,
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.ColumnDefinition>? columns,
        global::System.String name,
        global::Nimblesite.DataProvider.Migration.Core.PortableType type,
        global::System.Boolean isNullable,
        global::Nimblesite.DataProvider.Migration.Core.PrimaryKeyDefinition? primaryKey
    ) =>
        SchemaTestData.Table(
            schema: SchemaName,
            name: "tenants",
            columns:
            [
                SchemaTestData.Column(name: "id", type: PortableTypes.Uuid, isNullable: false),
            ],
            primaryKey: new PrimaryKeyDefinition { Columns = ["id"] }
        );

    private static global::Nimblesite.DataProvider.Migration.Core.TableDefinition Fixture_Schema_2(
        global::System.String schema,
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.ColumnDefinition>? columns,
        global::System.String name,
        global::Nimblesite.DataProvider.Migration.Core.PortableType type,
        global::System.Boolean isNullable,
        global::Nimblesite.DataProvider.Migration.Core.PrimaryKeyDefinition? primaryKey,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.ForeignKeyDefinition>? foreignKeys,
        global::System.Collections.Generic.IReadOnlyList<global::System.String>? columns,
        global::System.String referencedSchema,
        global::System.String referencedTable,
        global::System.Collections.Generic.IReadOnlyList<global::System.String>? referencedColumns,
        global::Nimblesite.DataProvider.Migration.Core.ForeignKeyAction onDelete,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.UniqueConstraintDefinition>? uniqueConstraints,
        global::System.Boolean includeUnique,
        global::System.String? name,
        global::System.Collections.Generic.IReadOnlyList<global::System.String>? columns
    ) =>
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
