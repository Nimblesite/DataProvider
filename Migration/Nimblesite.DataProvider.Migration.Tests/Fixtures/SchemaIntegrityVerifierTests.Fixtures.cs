// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SchemaIntegrityVerifierTests
{
    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Verify_EquivalentDefaultSchemasAndSqliteIntegerWidths_ReturnsNoMismatches_1() =>
        SchemaTestData.Schema(
            name: "live",
            tables:
            [
                SchemaTestData.Table(
                    schema: "main",
                    name: "items",
                    columns:
                    [
                        SchemaTestData.Column(
                            name: "id",
                            type: PortableTypes.BigInt,
                            isNullable: false,
                            defaultValue: "CURRENT_TIMESTAMP ;"
                        ),
                    ],
                    primaryKey: new PrimaryKeyDefinition { Columns = ["id"] },
                    indexes:
                    [
                        SchemaTestData.Index(
                            name: "idx_items_id",
                            columns: ["id"],
                            filter: "id IS NOT NULL"
                        ),
                    ]
                ),
            ]
        );

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Verify_EquivalentDefaultSchemasAndSqliteIntegerWidths_ReturnsNoMismatches_2() =>
        SchemaTestData.Schema(
            name: "desired",
            tables:
            [
                SchemaTestData.Table(
                    schema: "public",
                    name: "items",
                    columns:
                    [
                        SchemaTestData.Column(
                            name: "id",
                            type: PortableTypes.Int,
                            isNullable: false,
                            defaultValue: " current_timestamp"
                        ),
                    ],
                    primaryKey: new PrimaryKeyDefinition { Columns = ["ID"] },
                    indexes:
                    [
                        SchemaTestData.Index(
                            name: "IDX_ITEMS_ID",
                            columns: ["ID"],
                            filter: "ID is not null;"
                        ),
                    ]
                ),
            ]
        );

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Verify_WhenColumnsAndConstraintsDrift_ReturnsDetailedMismatches_1() =>
        SchemaTestData.Schema(
            name: "live",
            tables:
            [
                SchemaTestData.Table(
                    schema: "public",
                    name: "accounts",
                    columns:
                    [
                        SchemaTestData.Column(
                            name: "id",
                            type: PortableTypes.Int,
                            isNullable: true,
                            isIdentity: false
                        ),
                        SchemaTestData.Column(name: "tenant_id", type: PortableTypes.Uuid),
                        SchemaTestData.Column(
                            name: "code",
                            type: PortableTypes.VarChar(64),
                            defaultValue: "'old'"
                        ),
                        SchemaTestData.Column(name: "status", type: PortableTypes.VarChar(20)),
                    ],
                    primaryKey: new PrimaryKeyDefinition { Columns = ["tenant_id"] },
                    foreignKeys:
                    [
                        SchemaTestData.ForeignKey(
                            name: "fk_accounts_tenant",
                            columns: ["id"],
                            referencedTable: "organizations",
                            referencedColumns: ["tenant_key"],
                            onDelete: ForeignKeyAction.NoAction,
                            onUpdate: ForeignKeyAction.Cascade
                        ),
                    ],
                    uniqueConstraints:
                    [
                        SchemaTestData.UniqueConstraint(
                            name: "uq_accounts_tenant_code",
                            columns: ["code", "tenant_id"]
                        ),
                    ],
                    indexes:
                    [
                        SchemaTestData.Index(
                            name: "idx_accounts_code",
                            columns: ["tenant_id"],
                            expressions: ["lower(old_code)"],
                            filter: "code is not null"
                        ),
                    ],
                    checkConstraints:
                    [
                        SchemaTestData.CheckConstraint(
                            name: "ck_accounts_code",
                            expression: "length(code) > 1"
                        ),
                    ]
                ),
            ]
        );

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Verify_WhenColumnsAndConstraintsDrift_ReturnsDetailedMismatches_2() =>
        SchemaTestData.Schema(
            name: "desired",
            tables:
            [
                SchemaTestData.Table(schema: "public", name: "missing_table"),
                SchemaTestData.Table(
                    schema: "public",
                    name: "accounts",
                    columns:
                    [
                        SchemaTestData.Column(
                            name: "id",
                            type: PortableTypes.Uuid,
                            isNullable: false,
                            isIdentity: true,
                            defaultLqlExpression: "gen_uuid()"
                        ),
                        SchemaTestData.Column(
                            name: "code",
                            type: PortableTypes.VarChar(64),
                            defaultValue: "'new'"
                        ),
                        SchemaTestData.Column(
                            name: "status",
                            type: PortableTypes.VarChar(20),
                            checkConstraint: "status in ('active')"
                        ),
                        SchemaTestData.Column(name: "missing", type: PortableTypes.Text),
                    ],
                    primaryKey: new PrimaryKeyDefinition { Columns = ["id"] },
                    foreignKeys:
                    [
                        SchemaTestData.ForeignKey(
                            name: "fk_accounts_tenant",
                            columns: ["tenant_id"],
                            referencedTable: "tenants",
                            referencedColumns: ["id"],
                            onDelete: ForeignKeyAction.Cascade,
                            onUpdate: ForeignKeyAction.NoAction
                        ),
                        SchemaTestData.ForeignKey(
                            columns: ["missing_fk"],
                            referencedTable: "tenants",
                            referencedColumns: ["id"]
                        ),
                    ],
                    uniqueConstraints:
                    [
                        SchemaTestData.UniqueConstraint(
                            name: "uq_accounts_tenant_code",
                            columns: ["tenant_id", "code"]
                        ),
                        new UniqueConstraintDefinition { Columns = ["status"] },
                    ],
                    indexes:
                    [
                        SchemaTestData.Index(
                            name: "idx_accounts_code",
                            columns: ["code"],
                            expressions: ["lower(code)"],
                            isUnique: true,
                            filter: "code <> ''"
                        ),
                        SchemaTestData.Index(name: "idx_accounts_missing", columns: ["status"]),
                    ],
                    checkConstraints:
                    [
                        SchemaTestData.CheckConstraint(
                            name: "ck_accounts_code",
                            expression: "length(code) > 3"
                        ),
                        SchemaTestData.CheckConstraint(
                            name: "ck_accounts_missing",
                            expression: "status is not null"
                        ),
                    ]
                ),
            ]
        );

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Verify_WhenPrimaryKeyIsMissing_ReturnsMissingPrimaryKey_1() =>
        Schema
            .Define("desired")
            .Table(
                "public",
                "accounts",
                table => table.Column("id", PortableTypes.Uuid, column => column.PrimaryKey())
            )
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Verify_WhenRlsAndSupportObjectsDrift_ReturnsDetailedMismatches_1() =>
        SchemaTestData.Schema(
            name: "live",
            tables:
            [
                SchemaTestData.Table(
                    schema: "public",
                    name: "documents",
                    rowLevelSecurity: SchemaTestData.RlsPolicySet(
                        enabled: false,
                        forced: false,
                        policies: [new RlsPolicyDefinition { Name = "tenant_read_old" }]
                    )
                ),
            ],
            roles: [SchemaTestData.PostgresRole(name: "app_user", login: false, bypassRls: true)]
        );

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Verify_WhenRlsAndSupportObjectsDrift_ReturnsDetailedMismatches_2() =>
        SchemaTestData.Schema(
            name: "desired",
            tables:
            [
                SchemaTestData.Table(
                    schema: "public",
                    name: "documents",
                    rowLevelSecurity: SchemaTestData.RlsPolicySet(
                        enabled: true,
                        forced: true,
                        policies: [new RlsPolicyDefinition { Name = "tenant_read" }]
                    )
                ),
            ],
            roles:
            [
                SchemaTestData.PostgresRole(name: "app_user", login: true, bypassRls: false),
                new PostgresRoleDefinition { Name = "missing_role" },
            ],
            functions:
            [
                SchemaTestData.PostgresFunction(
                    schema: "public",
                    name: "current_tenant_id",
                    arguments: [new PostgresFunctionArgumentDefinition { Type = "text" }]
                ),
            ],
            grants:
            [
                SchemaTestData.PostgresGrant(
                    schema: "public",
                    target: PostgresGrantTarget.Table,
                    objectName: "documents",
                    privileges: ["select"],
                    roles: ["app_user"]
                ),
            ]
        );

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Verify_WhenRlsAndSupportObjectsAreExcluded_IgnoresThoseMismatches_1() =>
        SchemaTestData.Schema(
            name: "desired",
            tables:
            [
                SchemaTestData.Table(
                    schema: "public",
                    name: "documents",
                    rowLevelSecurity: SchemaTestData.RlsPolicySet(
                        enabled: true,
                        forced: true,
                        policies: [new RlsPolicyDefinition { Name = "tenant_read" }]
                    )
                ),
            ],
            roles: [new PostgresRoleDefinition { Name = "app_user" }],
            functions: [new PostgresFunctionDefinition { Name = "current_tenant_id" }],
            grants:
            [
                SchemaTestData.PostgresGrant(
                    schema: "public",
                    target: PostgresGrantTarget.Schema,
                    privileges: ["usage"],
                    roles: ["app_user"]
                ),
            ]
        );
}
