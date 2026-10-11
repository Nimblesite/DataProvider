// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class RlsYamlSerializerTests
{
    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_RlsPolicyDefinition_YamlRoundTrip_Simple_1(
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.TableDefinition>? tables,
        global::System.String schema,
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.ColumnDefinition>? columns,
        global::Nimblesite.DataProvider.Migration.Core.PortableType type,
        global::Nimblesite.DataProvider.Migration.Core.PrimaryKeyDefinition? primaryKey,
        global::Nimblesite.DataProvider.Migration.Core.RlsPolicySetDefinition? rowLevelSecurity,
        global::System.Boolean enabled,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.RlsPolicyDefinition>? policies,
        global::System.String name,
        global::System.Boolean isPermissive,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.RlsOperation> operations,
        global::System.String? usingLql,
        global::System.String? withCheckLql
    ) =>
        SchemaTestData.Schema(
            name: "test",
            tables:
            [
                SchemaTestData.Table(
                    schema: "public",
                    name: "Documents",
                    columns:
                    [
                        SchemaTestData.Column(name: "Id", type: new UuidType(), isNullable: false),
                        SchemaTestData.Column(
                            name: "OwnerId",
                            type: new UuidType(),
                            isNullable: false
                        ),
                    ],
                    primaryKey: new PrimaryKeyDefinition { Columns = ["Id"] },
                    rowLevelSecurity: SchemaTestData.RlsPolicySet(
                        enabled: true,
                        policies:
                        [
                            SchemaTestData.RlsPolicy(
                                name: "owner_isolation",
                                isPermissive: true,
                                operations: [RlsOperation.All],
                                usingLql: "OwnerId = current_user_id()",
                                withCheckLql: "OwnerId = current_user_id()"
                            ),
                        ]
                    )
                ),
            ]
        );

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_RlsOperation_AllValues_SerializeDeserialize_1(
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.TableDefinition>? tables,
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.ColumnDefinition>? columns,
        global::System.String name,
        global::Nimblesite.DataProvider.Migration.Core.PortableType type,
        global::Nimblesite.DataProvider.Migration.Core.RlsPolicySetDefinition? rowLevelSecurity,
        global::System.String name,
        global::Nimblesite.DataProvider.Migration.Core.RlsOperation op,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.RlsOperation> operations,
        global::System.String? usingLql
    ) =>
        SchemaTestData.Schema(
            name: "t",
            tables:
            [
                SchemaTestData.Table(
                    name: "T",
                    columns: [SchemaTestData.Column(name: "Id", type: new UuidType())],
                    rowLevelSecurity: new RlsPolicySetDefinition
                    {
                        Policies =
                        [
                            SchemaTestData.RlsPolicy(
                                name: $"p_{op}",
                                operations: [op],
                                usingLql: "Id = current_user_id()"
                            ),
                        ],
                    }
                ),
            ]
        );

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_RlsPolicy_RestrictiveAndRoles_RoundTrip_1(
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.ColumnDefinition>? columns,
        global::System.String name,
        global::Nimblesite.DataProvider.Migration.Core.PortableType type,
        global::Nimblesite.DataProvider.Migration.Core.RlsPolicySetDefinition? rowLevelSecurity,
        global::System.String name,
        global::System.Boolean isPermissive,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.RlsOperation> operations,
        global::System.Collections.Generic.IReadOnlyList<global::System.String>? roles,
        global::System.String? usingLql
    ) =>
        SchemaTestData.Schema(
            name: "t",
            tables:
            [
                SchemaTestData.Table(
                    name: "Audit",
                    columns: [SchemaTestData.Column(name: "Id", type: new UuidType())],
                    rowLevelSecurity: new RlsPolicySetDefinition
                    {
                        Policies =
                        [
                            SchemaTestData.RlsPolicy(
                                name: "audit_only_admins",
                                isPermissive: false,
                                operations: [RlsOperation.Select, RlsOperation.Delete],
                                roles: ["admin", "auditor"],
                                usingLql: "true"
                            ),
                        ],
                    }
                ),
            ]
        );

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_RlsPolicySet_DisabledFlag_RoundTrip_1(
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.TableDefinition>? tables,
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.ColumnDefinition>? columns,
        global::System.String name,
        global::Nimblesite.DataProvider.Migration.Core.PortableType type
    ) =>
        SchemaTestData.Schema(
            name: "t",
            tables:
            [
                SchemaTestData.Table(
                    name: "T",
                    columns: [SchemaTestData.Column(name: "Id", type: new UuidType())],
                    rowLevelSecurity: new RlsPolicySetDefinition { Enabled = false }
                ),
            ]
        );

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_RlsPolicySet_DefaultsOmittedFromYaml_1(
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.TableDefinition>? tables,
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.ColumnDefinition>? columns,
        global::System.String name,
        global::Nimblesite.DataProvider.Migration.Core.PortableType type,
        global::Nimblesite.DataProvider.Migration.Core.RlsPolicySetDefinition? rowLevelSecurity,
        global::System.String name,
        global::System.String? usingLql
    ) =>
        SchemaTestData.Schema(
            name: "t",
            tables:
            [
                SchemaTestData.Table(
                    name: "T",
                    columns: [SchemaTestData.Column(name: "Id", type: new UuidType())],
                    rowLevelSecurity: new RlsPolicySetDefinition
                    {
                        Policies =
                        [
                            SchemaTestData.RlsPolicy(name: "p", usingLql: "Id = current_user_id()"),
                        ],
                    }
                ),
            ]
        );

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_RowLevelSecurity_Absent_DoesNotAppearInYaml_1(
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.TableDefinition>? tables,
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.ColumnDefinition>? columns,
        global::System.String name,
        global::Nimblesite.DataProvider.Migration.Core.PortableType type
    ) =>
        SchemaTestData.Schema(
            name: "t",
            tables:
            [
                SchemaTestData.Table(
                    name: "Plain",
                    columns: [SchemaTestData.Column(name: "Id", type: new UuidType())]
                ),
            ]
        );
}
