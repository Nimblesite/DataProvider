// Implements [CI-DESLOP-INDEPENDENT-DATA].


namespace Nimblesite.DataProvider.Migration.Tests;

public partial class RlsYamlSerializerTests
{
    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_RlsPolicyDefinition_YamlRoundTrip_Simple_1() =>
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

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_RlsOperation_AllValues_SerializeDeserialize_1(global::Nimblesite.DataProvider.Migration.Core.RlsOperation op) =>
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

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_RlsPolicy_RestrictiveAndRoles_RoundTrip_1() =>
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

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_RlsPolicySet_DisabledFlag_RoundTrip_1() =>
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

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_RlsPolicySet_DefaultsOmittedFromYaml_1() =>
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

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_RowLevelSecurity_Absent_DoesNotAppearInYaml_1() =>
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
