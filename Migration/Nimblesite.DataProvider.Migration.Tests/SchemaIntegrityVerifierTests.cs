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

public sealed class SchemaIntegrityVerifierTests
{
    [Fact]
    public void Verify_EquivalentDefaultSchemasAndSqliteIntegerWidths_ReturnsNoMismatches()
    {
        var live = new SchemaDefinition
        {
            Name = "live",
            Tables =
            [
                new TableDefinition
                {
                    Schema = "main",
                    Name = "items",
                    Columns =
                    [
                        new ColumnDefinition
                        {
                            Name = "id",
                            Type = PortableTypes.BigInt,
                            IsNullable = false,
                            DefaultValue = "CURRENT_TIMESTAMP ;",
                        },
                    ],
                    PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
                    Indexes =
                    [
                        new IndexDefinition
                        {
                            Name = "idx_items_id",
                            Columns = ["id"],
                            Filter = "id IS NOT NULL",
                        },
                    ],
                },
            ],
        };
        var desired = new SchemaDefinition
        {
            Name = "desired",
            Tables =
            [
                new TableDefinition
                {
                    Schema = "public",
                    Name = "items",
                    Columns =
                    [
                        new ColumnDefinition
                        {
                            Name = "id",
                            Type = PortableTypes.Int,
                            IsNullable = false,
                            DefaultValue = " current_timestamp",
                        },
                    ],
                    PrimaryKey = new PrimaryKeyDefinition { Columns = ["ID"] },
                    Indexes =
                    [
                        new IndexDefinition
                        {
                            Name = "IDX_ITEMS_ID",
                            Columns = ["ID"],
                            Filter = "ID is not null;",
                        },
                    ],
                },
            ],
        };

        var mismatches = Verify(live: live, desired: desired);

        Assert.Empty(mismatches);
    }

    [Fact]
    public void Verify_WhenColumnsAndConstraintsDrift_ReturnsDetailedMismatches()
    {
        var live = new SchemaDefinition
        {
            Name = "live",
            Tables =
            [
                new TableDefinition
                {
                    Schema = "public",
                    Name = "accounts",
                    Columns =
                    [
                        new ColumnDefinition
                        {
                            Name = "id",
                            Type = PortableTypes.Int,
                            IsNullable = true,
                            IsIdentity = false,
                        },
                        new ColumnDefinition { Name = "tenant_id", Type = PortableTypes.Uuid },
                        new ColumnDefinition
                        {
                            Name = "code",
                            Type = PortableTypes.VarChar(64),
                            DefaultValue = "'old'",
                        },
                        new ColumnDefinition { Name = "status", Type = PortableTypes.VarChar(20) },
                    ],
                    PrimaryKey = new PrimaryKeyDefinition { Columns = ["tenant_id"] },
                    ForeignKeys =
                    [
                        new ForeignKeyDefinition
                        {
                            Name = "fk_accounts_tenant",
                            Columns = ["id"],
                            ReferencedTable = "organizations",
                            ReferencedColumns = ["tenant_key"],
                            OnDelete = ForeignKeyAction.NoAction,
                            OnUpdate = ForeignKeyAction.Cascade,
                        },
                    ],
                    UniqueConstraints =
                    [
                        new UniqueConstraintDefinition
                        {
                            Name = "uq_accounts_tenant_code",
                            Columns = ["code", "tenant_id"],
                        },
                    ],
                    Indexes =
                    [
                        new IndexDefinition
                        {
                            Name = "idx_accounts_code",
                            Columns = ["tenant_id"],
                            Expressions = ["lower(old_code)"],
                            Filter = "code is not null",
                        },
                    ],
                    CheckConstraints =
                    [
                        new CheckConstraintDefinition
                        {
                            Name = "ck_accounts_code",
                            Expression = "length(code) > 1",
                        },
                    ],
                },
            ],
        };
        var desired = new SchemaDefinition
        {
            Name = "desired",
            Tables =
            [
                new TableDefinition { Schema = "public", Name = "missing_table" },
                new TableDefinition
                {
                    Schema = "public",
                    Name = "accounts",
                    Columns =
                    [
                        new ColumnDefinition
                        {
                            Name = "id",
                            Type = PortableTypes.Uuid,
                            IsNullable = false,
                            IsIdentity = true,
                            DefaultLqlExpression = "gen_uuid()",
                        },
                        new ColumnDefinition
                        {
                            Name = "code",
                            Type = PortableTypes.VarChar(64),
                            DefaultValue = "'new'",
                        },
                        new ColumnDefinition
                        {
                            Name = "status",
                            Type = PortableTypes.VarChar(20),
                            CheckConstraint = "status in ('active')",
                        },
                        new ColumnDefinition { Name = "missing", Type = PortableTypes.Text },
                    ],
                    PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
                    ForeignKeys =
                    [
                        new ForeignKeyDefinition
                        {
                            Name = "fk_accounts_tenant",
                            Columns = ["tenant_id"],
                            ReferencedTable = "tenants",
                            ReferencedColumns = ["id"],
                            OnDelete = ForeignKeyAction.Cascade,
                            OnUpdate = ForeignKeyAction.NoAction,
                        },
                        new ForeignKeyDefinition
                        {
                            Columns = ["missing_fk"],
                            ReferencedTable = "tenants",
                            ReferencedColumns = ["id"],
                        },
                    ],
                    UniqueConstraints =
                    [
                        new UniqueConstraintDefinition
                        {
                            Name = "uq_accounts_tenant_code",
                            Columns = ["tenant_id", "code"],
                        },
                        new UniqueConstraintDefinition { Columns = ["status"] },
                    ],
                    Indexes =
                    [
                        new IndexDefinition
                        {
                            Name = "idx_accounts_code",
                            Columns = ["code"],
                            Expressions = ["lower(code)"],
                            IsUnique = true,
                            Filter = "code <> ''",
                        },
                        new IndexDefinition { Name = "idx_accounts_missing", Columns = ["status"] },
                    ],
                    CheckConstraints =
                    [
                        new CheckConstraintDefinition
                        {
                            Name = "ck_accounts_code",
                            Expression = "length(code) > 3",
                        },
                        new CheckConstraintDefinition
                        {
                            Name = "ck_accounts_missing",
                            Expression = "status is not null",
                        },
                    ],
                },
            ],
        };

        var mismatches = Verify(live: live, desired: desired);

        Assert.Contains("public.missing_table: missing table", mismatches);
        Assert.Contains(
            $"public.accounts.id: type expected {PortableTypes.Uuid} but found {PortableTypes.Int}",
            mismatches
        );
        Assert.Contains(
            "public.accounts.id: nullability expected NOT NULL but found NULL",
            mismatches
        );
        Assert.Contains("public.accounts.id: identity expected True but found False", mismatches);
        Assert.Contains(
            "public.accounts.id: default expected gen_uuid() but found <none>",
            mismatches
        );
        Assert.Contains("public.accounts.code: default expected 'new' but found 'old'", mismatches);
        Assert.Contains("public.accounts.missing: missing column", mismatches);
        Assert.Contains(
            "public.accounts: primary key columns expected (id) but found (tenant_id)",
            mismatches
        );
        Assert.Contains(
            "public.accounts: foreign key fk_accounts_tenant columns drifted",
            mismatches
        );
        Assert.Contains(
            "public.accounts: foreign key fk_accounts_tenant referenced table drifted",
            mismatches
        );
        Assert.Contains(
            "public.accounts: foreign key fk_accounts_tenant referenced columns drifted",
            mismatches
        );
        Assert.Contains(
            "public.accounts: foreign key fk_accounts_tenant on delete expected Cascade but found NoAction",
            mismatches
        );
        Assert.Contains(
            "public.accounts: foreign key fk_accounts_tenant on update expected NoAction but found Cascade",
            mismatches
        );
        Assert.Contains(
            "public.accounts: missing foreign key FK_accounts_missing_fk on (missing_fk)",
            mismatches
        );
        Assert.Contains(
            "public.accounts: unique constraint uq_accounts_tenant_code columns expected (tenant_id, code) but found (code, tenant_id)",
            mismatches
        );
        Assert.Contains(
            "public.accounts: missing unique constraint UQ_accounts_status on (status)",
            mismatches
        );
        Assert.Contains("public.accounts: index idx_accounts_code uniqueness drifted", mismatches);
        Assert.Contains("public.accounts: index idx_accounts_code columns drifted", mismatches);
        Assert.Contains("public.accounts: index idx_accounts_code expressions drifted", mismatches);
        Assert.Contains("public.accounts: index idx_accounts_code filter drifted", mismatches);
        Assert.Contains("public.accounts: missing index idx_accounts_missing", mismatches);
        Assert.Contains(
            "public.accounts: check constraint ck_accounts_code expression drifted",
            mismatches
        );
        Assert.Contains(
            "public.accounts: missing check constraint ck_accounts_missing",
            mismatches
        );
        Assert.Contains("public.accounts.status: missing check constraint", mismatches);
    }

    [Fact]
    public void Verify_WhenPrimaryKeyIsMissing_ReturnsMissingPrimaryKey()
    {
        var live = Schema
            .Define("live")
            .Table("public", "accounts", table => table.Column("id", PortableTypes.Uuid))
            .Build();
        var desired = Schema
            .Define("desired")
            .Table(
                "public",
                "accounts",
                table => table.Column("id", PortableTypes.Uuid, column => column.PrimaryKey())
            )
            .Build();

        var mismatches = Verify(live: live, desired: desired);

        Assert.Contains("public.accounts: missing primary key", mismatches);
    }

    [Fact]
    public void Verify_WhenRlsAndSupportObjectsDrift_ReturnsDetailedMismatches()
    {
        var live = new SchemaDefinition
        {
            Name = "live",
            Tables =
            [
                new TableDefinition
                {
                    Schema = "public",
                    Name = "documents",
                    RowLevelSecurity = new RlsPolicySetDefinition
                    {
                        Enabled = false,
                        Forced = false,
                        Policies = [new RlsPolicyDefinition { Name = "tenant_read_old" }],
                    },
                },
            ],
            Roles =
            [
                new PostgresRoleDefinition
                {
                    Name = "app_user",
                    Login = false,
                    BypassRls = true,
                },
            ],
        };
        var desired = new SchemaDefinition
        {
            Name = "desired",
            Tables =
            [
                new TableDefinition
                {
                    Schema = "public",
                    Name = "documents",
                    RowLevelSecurity = new RlsPolicySetDefinition
                    {
                        Enabled = true,
                        Forced = true,
                        Policies = [new RlsPolicyDefinition { Name = "tenant_read" }],
                    },
                },
            ],
            Roles =
            [
                new PostgresRoleDefinition
                {
                    Name = "app_user",
                    Login = true,
                    BypassRls = false,
                },
                new PostgresRoleDefinition { Name = "missing_role" },
            ],
            Functions =
            [
                new PostgresFunctionDefinition
                {
                    Schema = "public",
                    Name = "current_tenant_id",
                    Arguments = [new PostgresFunctionArgumentDefinition { Type = "text" }],
                },
            ],
            Grants =
            [
                new PostgresGrantDefinition
                {
                    Schema = "public",
                    Target = PostgresGrantTarget.Table,
                    ObjectName = "documents",
                    Privileges = ["select"],
                    Roles = ["app_user"],
                },
            ],
        };

        var mismatches = Verify(live: live, desired: desired);

        Assert.Contains(
            "public.documents: row-level security expected True but found False",
            mismatches
        );
        Assert.Contains(
            "public.documents: forced row-level security expected True but found False",
            mismatches
        );
        Assert.Contains(
            "public.documents: missing row-level security policy tenant_read",
            mismatches
        );
        Assert.Contains("role app_user: login drifted", mismatches);
        Assert.Contains("role app_user: bypassRls drifted", mismatches);
        Assert.Contains("role missing_role: missing role", mismatches);
        Assert.Contains("public.current_tenant_id: missing function", mismatches);
        Assert.Contains("grant Table documents: missing grant", mismatches);
    }

    [Fact]
    public void Verify_WhenRlsAndSupportObjectsAreExcluded_IgnoresThoseMismatches()
    {
        var live = new SchemaDefinition
        {
            Name = "live",
            Tables = [new TableDefinition { Schema = "public", Name = "documents" }],
        };
        var desired = new SchemaDefinition
        {
            Name = "desired",
            Tables =
            [
                new TableDefinition
                {
                    Schema = "public",
                    Name = "documents",
                    RowLevelSecurity = new RlsPolicySetDefinition
                    {
                        Enabled = true,
                        Forced = true,
                        Policies = [new RlsPolicyDefinition { Name = "tenant_read" }],
                    },
                },
            ],
            Roles = [new PostgresRoleDefinition { Name = "app_user" }],
            Functions = [new PostgresFunctionDefinition { Name = "current_tenant_id" }],
            Grants =
            [
                new PostgresGrantDefinition
                {
                    Schema = "public",
                    Target = PostgresGrantTarget.Schema,
                    Privileges = ["usage"],
                    Roles = ["app_user"],
                },
            ],
        };

        var mismatches = Verify(
            live: live,
            desired: desired,
            includeSupportObjects: false,
            includeRls: false
        );

        Assert.Empty(mismatches);
    }

    [Fact]
    public void Verify_WhenSchemaCannotBeRead_ReturnsError()
    {
        var result = SchemaIntegrityVerifier.Verify(
            live: Schema.Define("live").Build(),
            desired: new SchemaDefinition { Tables = null! },
            logger: NullLogger.Instance
        );

        Assert.True(result is SchemaIntegrityError);
    }

    private static ImmutableArray<string> Verify(
        SchemaDefinition live,
        SchemaDefinition desired,
        bool includeSupportObjects = true,
        bool includeRls = true
    )
    {
        var result = SchemaIntegrityVerifier.Verify(
            live: live,
            desired: desired,
            includeSupportObjects: includeSupportObjects,
            includeRls: includeRls,
            logger: NullLogger.Instance
        );

        Assert.True(
            result is SchemaIntegrityOk,
            "Expected schema integrity verification to succeed."
        );
        return ((SchemaIntegrityOk)result).Value;
    }
}
