// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresLqlOnlyE2ETests
{
    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlOnly_OrCombination_SelfOrOwner_1(
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.TableDefinition>? tables,
        global::System.String schema,
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.ColumnDefinition>? columns,
        global::System.String name,
        global::Nimblesite.DataProvider.Migration.Core.PortableType type,
        global::System.Boolean isNullable,
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

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlOnly_DifferentUsingVsWithCheck_ApiKeysShape_1(
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.TableDefinition>? tables,
        global::System.String schema,
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.ColumnDefinition>? columns,
        global::System.String name,
        global::Nimblesite.DataProvider.Migration.Core.PortableType type,
        global::System.Boolean isNullable,
        global::Nimblesite.DataProvider.Migration.Core.PrimaryKeyDefinition? primaryKey,
        global::Nimblesite.DataProvider.Migration.Core.RlsPolicySetDefinition? rowLevelSecurity,
        global::System.Boolean enabled,
        global::System.Boolean forced,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.RlsPolicyDefinition>? policies,
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.RlsOperation> operations,
        global::System.Collections.Generic.IReadOnlyList<global::System.String>? roles,
        global::System.String? usingLql,
        global::System.String? withCheckLql
    ) =>
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

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlOnly_NapMessagesShape_BareFnCallInLambdaBody_EnforcesIsolation_1(
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.TableDefinition>? tables,
        global::System.String schema,
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.ColumnDefinition>? columns,
        global::System.String name,
        global::Nimblesite.DataProvider.Migration.Core.PortableType type,
        global::System.Boolean isNullable,
        global::Nimblesite.DataProvider.Migration.Core.PrimaryKeyDefinition? primaryKey
    ) =>
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

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_LqlOnly_NapMessagesShape_BareFnCallInLambdaBody_EnforcesIsolation_2(
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.TableDefinition>? tables,
        global::System.String schema,
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.ColumnDefinition>? columns,
        global::System.String name,
        global::Nimblesite.DataProvider.Migration.Core.PortableType type,
        global::System.Boolean isNullable,
        global::Nimblesite.DataProvider.Migration.Core.PrimaryKeyDefinition? primaryKey,
        global::Nimblesite.DataProvider.Migration.Core.RlsPolicySetDefinition? rowLevelSecurity,
        global::System.Boolean enabled,
        global::System.Boolean forced,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.RlsPolicyDefinition>? policies,
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.RlsOperation> operations,
        global::System.Collections.Generic.IReadOnlyList<global::System.String>? roles,
        global::System.String? usingLql,
        global::System.String? withCheckLql
    ) =>
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
