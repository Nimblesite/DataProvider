// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresRlsNapShapeTests
{
    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_NapShape_OrCombinationPredicate_AppliesCorrectly_1(
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.TableDefinition>? tables,
        global::System.String schema,
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.ColumnDefinition>? columns,
        global::System.String name,
        global::Nimblesite.DataProvider.Migration.Core.PortableType type,
        global::System.Boolean isNullable,
        global::System.String? defaultValue,
        global::Nimblesite.DataProvider.Migration.Core.PrimaryKeyDefinition? primaryKey,
        global::Nimblesite.DataProvider.Migration.Core.RlsPolicySetDefinition? rowLevelSecurity,
        global::System.Boolean enabled,
        global::System.Boolean forced,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.RlsPolicyDefinition>? policies,
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.RlsOperation> operations,
        global::System.Collections.Generic.IReadOnlyList<global::System.String>? roles,
        global::System.String? usingLql
    ) =>
        SchemaTestData.Schema(
            name: "nap",
            tables:
            [
                SchemaTestData.Table(
                    schema: "public",
                    name: "tenant_members",
                    columns:
                    [
                        SchemaTestData.Column(name: "id", type: new UuidType(), isNullable: false),
                        SchemaTestData.Column(
                            name: "user_id",
                            type: new UuidType(),
                            isNullable: false
                        ),
                        SchemaTestData.Column(
                            name: "tenant_id",
                            type: new UuidType(),
                            isNullable: false
                        ),
                        SchemaTestData.Column(
                            name: "is_owner",
                            type: new BooleanType(),
                            isNullable: false,
                            defaultValue: "false"
                        ),
                    ],
                    primaryKey: new PrimaryKeyDefinition { Columns = ["id"] },
                    rowLevelSecurity: SchemaTestData.RlsPolicySet(
                        enabled: true,
                        forced: true,
                        policies:
                        [
                            SchemaTestData.RlsPolicy(
                                name: "tenant_members_self_or_owner",
                                operations: [RlsOperation.Select],
                                roles: ["nap_app_user"],
                                usingLql: "user_id = app_user_id() or (tenant_id = app_tenant_id() and is_owner)"
                            ),
                        ]
                    )
                ),
            ]
        );
}
