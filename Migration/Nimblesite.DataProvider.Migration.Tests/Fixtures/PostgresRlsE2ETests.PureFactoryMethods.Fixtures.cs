// Implements [CI-TEST-INDEPENDENT-SCHEMA-DATA].
// Pure immutable fixture definitions; assertions and database workflows remain analysed.

namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial class PostgresRlsE2ETests
{
    private static SchemaDefinition BuildOwnerIsolationSchema() =>
        SchemaTestData.Schema(
            name: "rls_test",
            tables:
            [
                SchemaTestData.Table(
                    schema: "public",
                    name: "documents",
                    columns:
                    [
                        SchemaTestData.Column(name: "id", type: new UuidType(), isNullable: false),
                        SchemaTestData.Column(
                            name: "owner_id",
                            type: new UuidType(),
                            isNullable: false
                        ),
                        SchemaTestData.Column(
                            name: "title",
                            type: new VarCharType(200),
                            isNullable: false
                        ),
                    ],
                    primaryKey: new PrimaryKeyDefinition { Columns = ["id"] },
                    rowLevelSecurity: new RlsPolicySetDefinition
                    {
                        Policies =
                        [
                            SchemaTestData.RlsPolicy(
                                name: "owner_isolation",
                                operations: [RlsOperation.All],
                                usingLql: "owner_id = current_user_id()::uuid",
                                withCheckLql: "owner_id = current_user_id()::uuid"
                            ),
                        ],
                    }
                ),
            ]
        );

    private static SchemaDefinition BuildGroupMembershipSchema() =>
        SchemaTestData.Schema(
            name: "rls_group_test",
            tables:
            [
                SchemaTestData.Table(
                    schema: "public",
                    name: "user_group_memberships",
                    columns:
                    [
                        SchemaTestData.Column(name: "id", type: new UuidType(), isNullable: false),
                        SchemaTestData.Column(
                            name: "user_id",
                            type: new VarCharType(450),
                            isNullable: false
                        ),
                        SchemaTestData.Column(
                            name: "group_id",
                            type: new UuidType(),
                            isNullable: false
                        ),
                    ],
                    primaryKey: new PrimaryKeyDefinition { Columns = ["id"] }
                ),
                SchemaTestData.Table(
                    schema: "public",
                    name: "documents",
                    columns:
                    [
                        SchemaTestData.Column(name: "id", type: new UuidType(), isNullable: false),
                        SchemaTestData.Column(
                            name: "group_id",
                            type: new UuidType(),
                            isNullable: false
                        ),
                        SchemaTestData.Column(
                            name: "title",
                            type: new VarCharType(200),
                            isNullable: false
                        ),
                    ],
                    primaryKey: new PrimaryKeyDefinition { Columns = ["id"] },
                    rowLevelSecurity: new RlsPolicySetDefinition
                    {
                        Policies =
                        [
                            SchemaTestData.RlsPolicy(
                                name: "group_read_access",
                                operations: [RlsOperation.Select],
                                usingLql: Data_BuildGroupMembershipSchema_1
                            ),
                        ],
                    }
                ),
            ]
        );
}
