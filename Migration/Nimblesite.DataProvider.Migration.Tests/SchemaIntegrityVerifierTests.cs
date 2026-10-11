// Implements [MIG-TEST-SCHEMA-FIXTURES].
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

public sealed partial class SchemaIntegrityVerifierTests
{
    [Fact]
    public void Verify_EquivalentDefaultSchemasAndSqliteIntegerWidths_ReturnsNoMismatches()
    {
        var live =
            Fixture_Verify_EquivalentDefaultSchemasAndSqliteIntegerWidths_ReturnsNoMismatches_1(
                name: name,
                tables: tables,
                schema: schema,
                name: name,
                columns: columns,
                name: name,
                type: type,
                isNullable: isNullable,
                defaultValue: defaultValue,
                primaryKey: primaryKey,
                indexes: indexes,
                name: name,
                columns: columns,
                filter: filter
            );
        var desired =
            Fixture_Verify_EquivalentDefaultSchemasAndSqliteIntegerWidths_ReturnsNoMismatches_2(
                name: name,
                tables: tables,
                schema: schema,
                name: name,
                columns: columns,
                name: name,
                type: type,
                isNullable: isNullable,
                defaultValue: defaultValue,
                primaryKey: primaryKey,
                indexes: indexes
            );

        var mismatches = Verify(live: live, desired: desired);

        Assert.Empty(mismatches);
    }

    [Fact]
    public void Verify_WhenColumnsAndConstraintsDrift_ReturnsDetailedMismatches()
    {
        var live = Fixture_Verify_WhenColumnsAndConstraintsDrift_ReturnsDetailedMismatches_1(
            name: name,
            tables: tables,
            schema: schema,
            name: name,
            columns: columns,
            name: name,
            type: type,
            isNullable: isNullable,
            isIdentity: isIdentity,
            defaultValue: defaultValue,
            primaryKey: primaryKey,
            foreignKeys: foreignKeys,
            name: name,
            columns: columns,
            referencedTable: referencedTable,
            referencedColumns: referencedColumns,
            onDelete: onDelete,
            onUpdate: onUpdate,
            uniqueConstraints: uniqueConstraints,
            name: name,
            columns: columns,
            indexes: indexes,
            name: name,
            columns: columns,
            expressions: expressions,
            filter: filter,
            checkConstraints: checkConstraints,
            name: name,
            expression: expression
        );
        var desired = Fixture_Verify_WhenColumnsAndConstraintsDrift_ReturnsDetailedMismatches_2(
            name: name,
            type: type,
            isNullable: isNullable,
            isIdentity: isIdentity,
            defaultLqlExpression: defaultLqlExpression,
            defaultValue: defaultValue,
            checkConstraint: checkConstraint,
            name: name,
            columns: columns,
            referencedTable: referencedTable,
            referencedColumns: referencedColumns,
            onDelete: onDelete,
            onUpdate: onUpdate,
            name: name,
            columns: columns,
            expressions: expressions,
            isUnique: isUnique,
            filter: filter,
            name: name,
            expression: expression
        );

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
        var desired = Fixture_Verify_WhenPrimaryKeyIsMissing_ReturnsMissingPrimaryKey_1();

        var mismatches = Verify(live: live, desired: desired);

        Assert.Contains("public.accounts: missing primary key", mismatches);
    }

    [Fact]
    public void Verify_WhenRlsAndSupportObjectsDrift_ReturnsDetailedMismatches()
    {
        var live = Fixture_Verify_WhenRlsAndSupportObjectsDrift_ReturnsDetailedMismatches_1(
            name: name,
            tables: tables,
            schema: schema,
            name: name,
            rowLevelSecurity: rowLevelSecurity,
            enabled: enabled,
            forced: forced,
            policies: policies,
            roles: roles,
            name: name,
            login: login,
            bypassRls: bypassRls
        );
        var desired = Fixture_Verify_WhenRlsAndSupportObjectsDrift_ReturnsDetailedMismatches_2(
            name: name,
            tables: tables,
            schema: schema,
            name: name,
            rowLevelSecurity: rowLevelSecurity,
            enabled: enabled,
            forced: forced,
            policies: policies,
            roles: roles,
            name: name,
            login: login,
            bypassRls: bypassRls,
            functions: functions,
            schema: schema,
            name: name,
            arguments: arguments,
            grants: grants,
            schema: schema,
            target: target,
            objectName: objectName,
            privileges: privileges,
            roles: roles
        );

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
        var live = SchemaTestData.Schema(
            name: "live",
            tables: [SchemaTestData.Table(schema: "public", name: "documents")]
        );
        var desired = Fixture_Verify_WhenRlsAndSupportObjectsAreExcluded_IgnoresThoseMismatches_1(
            name: name,
            tables: tables,
            schema: schema,
            name: name,
            rowLevelSecurity: rowLevelSecurity,
            enabled: enabled,
            forced: forced,
            policies: policies,
            roles: roles,
            functions: functions,
            grants: grants,
            schema: schema,
            target: target,
            privileges: privileges,
            roles: roles
        );

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
