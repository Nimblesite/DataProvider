using static global::Tests.Shared.StringAssertions;

// Implements [MIG-TEST-SCHEMA-FIXTURES].
namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [RLS-PG] DDL string-shape tests from docs/specs/rls-spec.md.

/// <summary>
/// String-assertion tests for the PostgreSQL RLS DDL generator. Verifies the
/// shape of emitted SQL without needing a live database.
/// </summary>
public sealed partial class PostgresRlsDdlTests
{
    [Fact]
    public void Generate_EnableRls_EmitsAlterTableEnableRowLevelSecurity()
    {
        var ddl = PostgresDdlGenerator.Generate(new EnableRlsOperation("public", "Documents"));
        Assert.Equal("ALTER TABLE \"public\".\"Documents\" ENABLE ROW LEVEL SECURITY", ddl);
    }

    [Fact]
    public void Generate_DisableRls_EmitsAlterTableDisableRowLevelSecurity()
    {
        var ddl = PostgresDdlGenerator.Generate(new DisableRlsOperation("public", "Documents"));
        Assert.Equal("ALTER TABLE \"public\".\"Documents\" DISABLE ROW LEVEL SECURITY", ddl);
    }

    [Fact]
    public void Generate_EnableForceRls_EmitsAlterTableForceRowLevelSecurity()
    {
        var ddl = PostgresDdlGenerator.Generate(new EnableForceRlsOperation("public", "Documents"));
        Assert.Equal("ALTER TABLE \"public\".\"Documents\" FORCE ROW LEVEL SECURITY", ddl);
    }

    [Fact]
    public void Generate_DisableForceRls_EmitsAlterTableNoForceRowLevelSecurity()
    {
        var ddl = PostgresDdlGenerator.Generate(
            new DisableForceRlsOperation("public", "Documents")
        );
        Assert.Equal("ALTER TABLE \"public\".\"Documents\" NO FORCE ROW LEVEL SECURITY", ddl);
    }

    [Fact]
    public void Generate_DropRlsPolicy_EmitsDropPolicyIfExists()
    {
        var ddl = PostgresDdlGenerator.Generate(
            new DropRlsPolicyOperation("public", "Documents", "owner_isolation")
        );
        Assert.Equal("DROP POLICY IF EXISTS \"owner_isolation\" ON \"public\".\"Documents\"", ddl);
    }

    [Fact]
    public void Generate_CreateRlsPolicy_OwnerIsolation_FullShape()
    {
        var ddl = PostgresDdlGenerator.Generate(
            new CreateRlsPolicyOperation(
                "public",
                "Documents",
                Fixture_Generate_CreateRlsPolicy_OwnerIsolation_FullShape_1()
            )
        );

        ContainsAll(
            actual: ddl,
            expected:
            [
                "CREATE POLICY \"owner_isolation\" ON \"public\".\"Documents\"",
                "AS PERMISSIVE",
                "FOR ALL",
                "TO PUBLIC",
                "USING (",
                "WITH CHECK (",
                "\"OwnerId\"",
                "current_setting('rls.current_user_id', true)",
            ],
            comparison: StringComparison.Ordinal
        );
    }

    [Fact]
    public void Generate_CreateRlsPolicy_RestrictiveSelectOnly_OnSpecificRoles()
    {
        var ddl = PostgresDdlGenerator.Generate(
            new CreateRlsPolicyOperation(
                "public",
                "Audit",
                Fixture_Generate_CreateRlsPolicy_RestrictiveSelectOnly_OnSpecificRoles_1()
            )
        );

        ContainsAll(
            actual: ddl,
            expected: ["AS RESTRICTIVE", "FOR SELECT", "TO \"admin\", \"auditor\"", "USING ("],
            comparison: StringComparison.Ordinal
        );
        Assert.DoesNotContain("WITH CHECK", ddl, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_CreateRlsPolicy_LqlExistsSubquery_TranspilesAndWraps()
    {
        var lql = Data_Generate_CreateRlsPolicy_LqlExistsSubquery_TranspilesAndWraps_1;

        var ddl = PostgresDdlGenerator.Generate(
            new CreateRlsPolicyOperation(
                "public",
                "Documents",
                Fixture_Generate_CreateRlsPolicy_LqlExistsSubquery_TranspilesAndWraps_1(lql: lql)
            )
        );

        ContainsAll(
            actual: ddl,
            expected:
            [
                "USING (EXISTS (",
                "current_setting('rls.current_user_id', true)",
                "UserGroupMemberships",
            ],
            comparison: StringComparison.Ordinal
        );
    }

    [Fact]
    public void Generate_CreateRlsPolicy_RawSqlPredicates_EmitVerbatim()
    {
        var ddl = PostgresDdlGenerator.Generate(
            new CreateRlsPolicyOperation(
                "public",
                "Documents",
                Fixture_Generate_CreateRlsPolicy_RawSqlPredicates_EmitVerbatim_1()
            )
        );

        ContainsAll(
            actual: ddl,
            expected: ["USING (is_member(\"GroupId\"))", "WITH CHECK (can_write(\"GroupId\"))"],
            comparison: StringComparison.Ordinal
        );
        Assert.DoesNotContain(
            "current_setting('rls.current_user_id', true)",
            ddl,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Generate_CreateRlsPolicy_EmptyPredicate_ThrowsWithRlsErrorCode()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            PostgresDdlGenerator.Generate(
                new CreateRlsPolicyOperation(
                    "public",
                    "Documents",
                    SchemaTestData.RlsPolicy(name: "broken", usingLql: "")
                )
            )
        );
        Assert.Contains("MIG-E-RLS-EMPTY-PREDICATE", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_CreateRlsPolicy_EmptyWithCheck_ThrowsWithRlsErrorCode()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            PostgresDdlGenerator.Generate(
                new CreateRlsPolicyOperation(
                    "public",
                    "Documents",
                    SchemaTestData.RlsPolicy(
                        name: "broken_check",
                        operations: [RlsOperation.Insert]
                    )
                )
            )
        );
        Assert.Contains("MIG-E-RLS-EMPTY-CHECK", ex.Message, StringComparison.Ordinal);
    }
}
