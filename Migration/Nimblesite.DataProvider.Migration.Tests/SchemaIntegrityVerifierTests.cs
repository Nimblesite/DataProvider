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
            Fixture_Verify_EquivalentDefaultSchemasAndSqliteIntegerWidths_ReturnsNoMismatches_1();
        var desired =
            Fixture_Verify_EquivalentDefaultSchemasAndSqliteIntegerWidths_ReturnsNoMismatches_2();

        var mismatches = Verify(live: live, desired: desired);

        Assert.Empty(mismatches);
    }

    [Fact]
    public void Verify_WhenColumnsAndConstraintsDrift_ReturnsDetailedMismatches()
    {
        var live = Fixture_Verify_WhenColumnsAndConstraintsDrift_ReturnsDetailedMismatches_1();
        var desired = Fixture_Verify_WhenColumnsAndConstraintsDrift_ReturnsDetailedMismatches_2();

        var mismatches = Verify(live: live, desired: desired);

        Assert.Contains("public.missing_table: missing table", mismatches);
        Assert.Contains(
            $"public.accounts.id: type expected {PortableTypes.Uuid} but found {PortableTypes.Int}",
            mismatches
        );
        AssertIntegrityMismatchMessages(
            input0: mismatches,
            value0: "public.accounts.id: nullability expected NOT NULL but found NULL",
            value1: "public.accounts.id: identity expected True but found False",
            value2: "public.accounts.id: default expected gen_uuid() but found <none>",
            value3: "public.accounts.code: default expected 'new' but found 'old'",
            value4: "public.accounts.missing: missing column",
            value5: "public.accounts: primary key columns expected (id) but found (tenant_id)",
            value6: "public.accounts: foreign key fk_accounts_tenant columns drifted"
        );
        AssertIntegrityMismatchMessages(
            input0: mismatches,
            value0: "public.accounts: foreign key fk_accounts_tenant referenced table drifted",
            value1: "public.accounts: foreign key fk_accounts_tenant referenced columns drifted",
            value2: "public.accounts: foreign key fk_accounts_tenant on delete expected Cascade but found NoAction",
            value3: "public.accounts: foreign key fk_accounts_tenant on update expected NoAction but found Cascade",
            value4: "public.accounts: missing foreign key FK_accounts_missing_fk on (missing_fk)",
            value5: "public.accounts: unique constraint uq_accounts_tenant_code columns expected (tenant_id, code) but found (code, tenant_id)",
            value6: "public.accounts: missing unique constraint UQ_accounts_status on (status)"
        );
        AssertIntegrityMismatchMessages(
            input0: mismatches,
            value0: "public.accounts: index idx_accounts_code uniqueness drifted",
            value1: "public.accounts: index idx_accounts_code columns drifted",
            value2: "public.accounts: index idx_accounts_code expressions drifted",
            value3: "public.accounts: index idx_accounts_code filter drifted",
            value4: "public.accounts: missing index idx_accounts_missing",
            value5: "public.accounts: check constraint ck_accounts_code expression drifted",
            value6: "public.accounts: missing check constraint ck_accounts_missing"
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
        var live = Fixture_Verify_WhenRlsAndSupportObjectsDrift_ReturnsDetailedMismatches_1();
        var desired = Fixture_Verify_WhenRlsAndSupportObjectsDrift_ReturnsDetailedMismatches_2();

        var mismatches = Verify(live: live, desired: desired);

        AssertIntegrityMismatchMessages(
            input0: mismatches,
            value0: "public.documents: row-level security expected True but found False",
            value1: "public.documents: forced row-level security expected True but found False",
            value2: "public.documents: missing row-level security policy tenant_read",
            value3: "role app_user: login drifted",
            value4: "role app_user: bypassRls drifted",
            value5: "role missing_role: missing role",
            value6: "public.current_tenant_id: missing function"
        );
        Assert.Contains("grant Table documents: missing grant", mismatches);
    }

    [Fact]
    public void Verify_WhenRlsAndSupportObjectsAreExcluded_IgnoresThoseMismatches()
    {
        var live = SchemaTestData.Schema(
            name: "live",
            tables: [SchemaTestData.Table(schema: "public", name: "documents")]
        );
        var desired = Fixture_Verify_WhenRlsAndSupportObjectsAreExcluded_IgnoresThoseMismatches_1();

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

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void AssertIntegrityMismatchMessages(
        global::System.Collections.Immutable.ImmutableArray<global::System.String> input0,
        string value0,
        string value1,
        string value2,
        string value3,
        string value4,
        string value5,
        string value6
    )
    {
        Assert.Contains(value0, input0);
        Assert.Contains(value1, input0);
        Assert.Contains(value2, input0);
        Assert.Contains(value3, input0);
        Assert.Contains(value4, input0);
        Assert.Contains(value5, input0);
        Assert.Contains(value6, input0);
    }
}
