// Implements [CI-DESLOP-INDEPENDENT-DATA].


namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresRlsNapShapeTests
{
    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_NapShape_OrCombinationPredicate_AppliesCorrectly_1() =>
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
