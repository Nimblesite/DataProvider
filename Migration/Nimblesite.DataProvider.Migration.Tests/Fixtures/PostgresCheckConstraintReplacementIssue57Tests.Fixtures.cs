// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresCheckConstraintReplacementIssue57Tests
{
    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_SchemaWithKindCheck_1(
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.TableDefinition>? tables,
        global::System.String schema,
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.ColumnDefinition>? columns,
        global::System.String name,
        global::Nimblesite.DataProvider.Migration.Core.PortableType type,
        global::System.Boolean isNullable,
        global::Nimblesite.DataProvider.Migration.Core.PrimaryKeyDefinition? primaryKey,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.CheckConstraintDefinition>? checkConstraints,
        global::System.String name,
        global::System.String expression,
        global::System.String expression
    ) =>
        SchemaTestData.Schema(
            name: "issue57",
            tables:
            [
                SchemaTestData.Table(
                    schema: SchemaName,
                    name: "usage_events",
                    columns:
                    [
                        SchemaTestData.Column(
                            name: "id",
                            type: PortableTypes.Uuid,
                            isNullable: false
                        ),
                        SchemaTestData.Column(
                            name: "kind",
                            type: PortableTypes.Text,
                            isNullable: false
                        ),
                    ],
                    primaryKey: new PrimaryKeyDefinition { Columns = ["id"] },
                    checkConstraints:
                    [
                        SchemaTestData.CheckConstraint(
                            name: "usage_events_kind_check",
                            expression: expression
                        ),
                    ]
                ),
            ]
        );
}
