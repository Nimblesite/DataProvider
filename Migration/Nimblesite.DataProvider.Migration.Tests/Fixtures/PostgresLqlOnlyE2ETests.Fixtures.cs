// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresLqlOnlyE2ETests
{
    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlOnly_OrCombination_SelfOrOwner_1() =>
        SchemaTestData.Schema(
            name: "lql",
            tables:
            [
                SchemaTestData.Table(
                    schema: "public",
                    name: "self_owner",
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
                    ],
                    primaryKey: new PrimaryKeyDefinition { Columns = ["id"] },
                    rowLevelSecurity: SchemaTestData.RlsPolicySet(
                        enabled: true,
                        forced: true,
                        policies:
                        [
                            SchemaTestData.RlsPolicy(
                                name: "self_or_member",
                                operations: [RlsOperation.Select],
                                roles: ["lql_user"],
                                usingLql: "user_id = app_user_id() or (tenant_id = app_tenant_id() and is_member(app_user_id(), app_tenant_id()))"
                            ),
                        ]
                    )
                ),
            ]
        );

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlOnly_DifferentUsingVsWithCheck_ApiKeysShape_1() =>
        SchemaTestData.Schema(
            name: "lql",
            tables:
            [
                SchemaTestData.Table(
                    schema: "public",
                    name: "asymmetric",
                    columns:
                    [
                        SchemaTestData.Column(name: "id", type: new UuidType(), isNullable: false),
                        SchemaTestData.Column(
                            name: "tenant_id",
                            type: new UuidType(),
                            isNullable: false
                        ),
                    ],
                    primaryKey: new PrimaryKeyDefinition { Columns = ["id"] },
                    rowLevelSecurity: SchemaTestData.RlsPolicySet(
                        enabled: true,
                        forced: true,
                        policies:
                        [
                            SchemaTestData.RlsPolicy(
                                name: "asym_pol",
                                operations: [RlsOperation.All],
                                roles: ["lql_user"],
                                usingLql: "is_member(app_user_id(), app_tenant_id())",
                                withCheckLql: "is_member(app_user_id(), app_tenant_id()) and tenant_id = app_tenant_id()"
                            ),
                        ]
                    )
                ),
            ]
        );

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlOnly_NapMessagesShape_BareFnCallInLambdaBody_EnforcesIsolation_1() =>
        SchemaTestData.Schema(
            name: "lql",
            tables:
            [
                SchemaTestData.Table(
                    schema: "public",
                    name: "conversations",
                    columns:
                    [
                        SchemaTestData.Column(name: "id", type: new UuidType(), isNullable: false),
                        SchemaTestData.Column(
                            name: "tenant_id",
                            type: new UuidType(),
                            isNullable: false
                        ),
                    ],
                    primaryKey: new PrimaryKeyDefinition { Columns = ["id"] }
                ),
            ]
        );

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlOnly_NapMessagesShape_BareFnCallInLambdaBody_EnforcesIsolation_2() =>
        SchemaTestData.Schema(
            name: "lql",
            tables:
            [
                SchemaTestData.Table(
                    schema: "public",
                    name: "messages",
                    columns:
                    [
                        SchemaTestData.Column(name: "id", type: new UuidType(), isNullable: false),
                        SchemaTestData.Column(
                            name: "conversation_id",
                            type: new UuidType(),
                            isNullable: false
                        ),
                        SchemaTestData.Column(
                            name: "body",
                            type: new TextType(),
                            isNullable: false
                        ),
                    ],
                    primaryKey: new PrimaryKeyDefinition { Columns = ["id"] },
                    rowLevelSecurity: SchemaTestData.RlsPolicySet(
                        enabled: true,
                        forced: true,
                        policies:
                        [
                            SchemaTestData.RlsPolicy(
                                name: "messages_member",
                                operations: [RlsOperation.All],
                                roles: ["lql_user"],
                                usingLql: "exists(conversations |> filter(fn(c) => c.id = conversation_id and is_member(app_user_id(), c.tenant_id)))",
                                withCheckLql: "exists(conversations |> filter(fn(c) => c.id = conversation_id and is_member(app_user_id(), c.tenant_id)))"
                            ),
                        ]
                    )
                ),
            ]
        );
}
