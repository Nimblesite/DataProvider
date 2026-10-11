// Implements [CI-DESLOP-INDEPENDENT-DATA].
using System.Collections.Immutable;
using SchemaIntegrityError = Outcome.Result<
    System.Collections.Immutable.ImmutableArray<string>,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Error<
    System.Collections.Immutable.ImmutableArray<string>,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>;
using SchemaIntegrityOk = Outcome.Result<
    System.Collections.Immutable.ImmutableArray<string>,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Ok<
    System.Collections.Immutable.ImmutableArray<string>,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>;

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SchemaIntegrityVerifierTests
{
    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Verify_EquivalentDefaultSchemasAndSqliteIntegerWidths_ReturnsNoMismatches_1(
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
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.IndexDefinition>? indexes,
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::System.String>? columns,
        global::System.String? filter
    ) =>
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

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Verify_EquivalentDefaultSchemasAndSqliteIntegerWidths_ReturnsNoMismatches_2(
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
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.IndexDefinition>? indexes
    ) =>
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

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Verify_WhenColumnsAndConstraintsDrift_ReturnsDetailedMismatches_1(
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.TableDefinition>? tables,
        global::System.String schema,
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.ColumnDefinition>? columns,
        global::System.String name,
        global::Nimblesite.DataProvider.Migration.Core.PortableType type,
        global::System.Boolean isNullable,
        global::System.Boolean isIdentity,
        global::System.String? defaultValue,
        global::Nimblesite.DataProvider.Migration.Core.PrimaryKeyDefinition? primaryKey,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.ForeignKeyDefinition>? foreignKeys,
        global::System.String? name,
        global::System.Collections.Generic.IReadOnlyList<global::System.String>? columns,
        global::System.String referencedTable,
        global::System.Collections.Generic.IReadOnlyList<global::System.String>? referencedColumns,
        global::Nimblesite.DataProvider.Migration.Core.ForeignKeyAction onDelete,
        global::Nimblesite.DataProvider.Migration.Core.ForeignKeyAction onUpdate,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.UniqueConstraintDefinition>? uniqueConstraints,
        global::System.String? name,
        global::System.Collections.Generic.IReadOnlyList<global::System.String>? columns,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.IndexDefinition>? indexes,
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::System.String>? columns,
        global::System.Collections.Generic.IReadOnlyList<global::System.String>? expressions,
        global::System.String? filter,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.CheckConstraintDefinition>? checkConstraints,
        global::System.String name,
        global::System.String expression
    ) =>
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

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Verify_WhenColumnsAndConstraintsDrift_ReturnsDetailedMismatches_2(
        global::System.String name,
        global::Nimblesite.DataProvider.Migration.Core.PortableType type,
        global::System.Boolean isNullable,
        global::System.Boolean isIdentity,
        global::System.String? defaultLqlExpression,
        global::System.String? defaultValue,
        global::System.String? checkConstraint,
        global::System.String? name,
        global::System.Collections.Generic.IReadOnlyList<global::System.String>? columns,
        global::System.String referencedTable,
        global::System.Collections.Generic.IReadOnlyList<global::System.String>? referencedColumns,
        global::Nimblesite.DataProvider.Migration.Core.ForeignKeyAction onDelete,
        global::Nimblesite.DataProvider.Migration.Core.ForeignKeyAction onUpdate,
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::System.String>? columns,
        global::System.Collections.Generic.IReadOnlyList<global::System.String>? expressions,
        global::System.Boolean isUnique,
        global::System.String? filter,
        global::System.String name,
        global::System.String expression
    ) =>
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

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Verify_WhenPrimaryKeyIsMissing_ReturnsMissingPrimaryKey_1() =>
        Schema
            .Define("desired")
            .Table(
                "public",
                "accounts",
                table => table.Column("id", PortableTypes.Uuid, column => column.PrimaryKey())
            )
            .Build();

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Verify_WhenRlsAndSupportObjectsDrift_ReturnsDetailedMismatches_1(
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.TableDefinition>? tables,
        global::System.String schema,
        global::System.String name,
        global::Nimblesite.DataProvider.Migration.Core.RlsPolicySetDefinition? rowLevelSecurity,
        global::System.Boolean enabled,
        global::System.Boolean forced,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.RlsPolicyDefinition>? policies,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.PostgresRoleDefinition>? roles,
        global::System.String name,
        global::System.Boolean login,
        global::System.Boolean bypassRls
    ) =>
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

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Verify_WhenRlsAndSupportObjectsDrift_ReturnsDetailedMismatches_2(
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.TableDefinition>? tables,
        global::System.String schema,
        global::System.String name,
        global::Nimblesite.DataProvider.Migration.Core.RlsPolicySetDefinition? rowLevelSecurity,
        global::System.Boolean enabled,
        global::System.Boolean forced,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.RlsPolicyDefinition>? policies,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.PostgresRoleDefinition>? roles,
        global::System.String name,
        global::System.Boolean login,
        global::System.Boolean bypassRls,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.PostgresFunctionDefinition>? functions,
        global::System.String schema,
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.PostgresFunctionArgumentDefinition>? arguments,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.PostgresGrantDefinition>? grants,
        global::System.String schema,
        global::Nimblesite.DataProvider.Migration.Core.PostgresGrantTarget target,
        global::System.String? objectName,
        global::System.Collections.Generic.IReadOnlyList<global::System.String>? privileges,
        global::System.Collections.Generic.IReadOnlyList<global::System.String>? roles
    ) =>
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

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Verify_WhenRlsAndSupportObjectsAreExcluded_IgnoresThoseMismatches_1(
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.TableDefinition>? tables,
        global::System.String schema,
        global::System.String name,
        global::Nimblesite.DataProvider.Migration.Core.RlsPolicySetDefinition? rowLevelSecurity,
        global::System.Boolean enabled,
        global::System.Boolean forced,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.RlsPolicyDefinition>? policies,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.PostgresRoleDefinition>? roles,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.PostgresFunctionDefinition>? functions,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.PostgresGrantDefinition>? grants,
        global::System.String schema,
        global::Nimblesite.DataProvider.Migration.Core.PostgresGrantTarget target,
        global::System.Collections.Generic.IReadOnlyList<global::System.String>? privileges,
        global::System.Collections.Generic.IReadOnlyList<global::System.String>? roles
    ) =>
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
