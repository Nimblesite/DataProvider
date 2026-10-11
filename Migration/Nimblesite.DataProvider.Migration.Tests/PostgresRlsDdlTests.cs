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
            Dataset_Generate_CreateRlsPolicy_OwnerIsolation_FullShape_1()
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
            Dataset_Generate_CreateRlsPolicy_RestrictiveSelectOnly_OnSpecificRoles_1()
        );

        VerifyReusableWorkflow17(
            input0: ContainsAll,
            input1: ddl,
            value0: "AS RESTRICTIVE",
            value1: "FOR SELECT",
            value2: "TO \"admin\", \"auditor\"",
            value3: "USING (",
            value5: "WITH CHECK"
        );
    }

    [Fact]
    public void Generate_CreateRlsPolicy_LqlExistsSubquery_TranspilesAndWraps()
    {
        var lql = Data_Generate_CreateRlsPolicy_LqlExistsSubquery_TranspilesAndWraps_1;

        var ddl = PostgresDdlGenerator.Generate(
            Dataset_Generate_CreateRlsPolicy_LqlExistsSubquery_TranspilesAndWraps_1(lql: lql)
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
            Dataset_Generate_CreateRlsPolicy_RawSqlPredicates_EmitVerbatim_1()
        );

        global::Nimblesite.DataProvider.Migration.Tests.RlsCurrentSettingLqlTests.VerifyCrossClassChecks2(
            input0: ddl,
            value0: "USING (is_member(\"GroupId\"))",
            value1: "WITH CHECK (can_write(\"GroupId\"))",
            value3: "current_setting('rls.current_user_id', true)"
        );
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(
        nameof(Generate_CreateRlsPolicy_EmptyPredicate_ThrowsWithRlsErrorCode_ContextCasesData)
    )]
    public void Generate_CreateRlsPolicy_EmptyPredicate_ThrowsWithRlsErrorCode_ContextCases(
        string caseName,
        Func<CreateRlsPolicyOperation> input0,
        string value0
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: caseName));
        ExerciseProjectionCaseMatrix6(input0: input0, value0: value0);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyReusableWorkflow17(
        Action<
            string?,
            System.Collections.Immutable.ImmutableArray<string>,
            StringComparison
        > input0,
        string input1,
        string value0,
        string value1,
        string value2,
        string value3,
        string value5
    )
    {
        input0(
            arg1: input1,
            arg2: [value0, value1, value2, value3],
            arg3: StringComparison.Ordinal
        );
        Assert.DoesNotContain(value5, input1, StringComparison.Ordinal);
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    private static void ExerciseProjectionCaseMatrix6(
        Func<CreateRlsPolicyOperation> input0,
        string value0
    )
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            PostgresDdlGenerator.Generate(input0())
        );
        Assert.Contains(value0, ex.Message, StringComparison.Ordinal);
    }
}
