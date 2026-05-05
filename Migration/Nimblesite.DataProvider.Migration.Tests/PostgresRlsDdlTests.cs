namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [RLS-PG] DDL string-shape tests from docs/specs/rls-spec.md.

/// <summary>
/// String-assertion tests for the PostgreSQL RLS DDL generator. Verifies the
/// shape of emitted SQL without needing a live database.
/// </summary>
public sealed class PostgresRlsDdlTests
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
                new RlsPolicyDefinition
                {
                    Name = "owner_isolation",
                    IsPermissive = true,
                    Operations = [RlsOperation.All],
                    UsingLql = "OwnerId = current_user_id()",
                    WithCheckLql = "OwnerId = current_user_id()",
                }
            )
        );

        Assert.Contains(
            "CREATE POLICY \"owner_isolation\" ON \"public\".\"Documents\"",
            ddl,
            StringComparison.Ordinal
        );
        Assert.Contains("AS PERMISSIVE", ddl, StringComparison.Ordinal);
        Assert.Contains("FOR ALL", ddl, StringComparison.Ordinal);
        Assert.Contains("TO PUBLIC", ddl, StringComparison.Ordinal);
        Assert.Contains("USING (", ddl, StringComparison.Ordinal);
        Assert.Contains("WITH CHECK (", ddl, StringComparison.Ordinal);
        Assert.Contains("\"OwnerId\"", ddl, StringComparison.Ordinal);
        Assert.Contains(
            "current_setting('rls.current_user_id', true)",
            ddl,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Generate_CreateRlsPolicy_RestrictiveSelectOnly_OnSpecificRoles()
    {
        var ddl = PostgresDdlGenerator.Generate(
            new CreateRlsPolicyOperation(
                "public",
                "Audit",
                new RlsPolicyDefinition
                {
                    Name = "audit_admin_only",
                    IsPermissive = false,
                    Operations = [RlsOperation.Select],
                    Roles = ["admin", "auditor"],
                    UsingLql = "true",
                }
            )
        );

        Assert.Contains("AS RESTRICTIVE", ddl, StringComparison.Ordinal);
        Assert.Contains("FOR SELECT", ddl, StringComparison.Ordinal);
        Assert.Contains("TO \"admin\", \"auditor\"", ddl, StringComparison.Ordinal);
        Assert.Contains("USING (", ddl, StringComparison.Ordinal);
        Assert.DoesNotContain("WITH CHECK", ddl, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_CreateRlsPolicy_LqlExistsSubquery_TranspilesAndWraps()
    {
        var lql = """
            UserGroupMemberships
            |> filter(fn(m) => m.user_id = current_user_id())
            |> select(m.user_id)
            """;

        var ddl = PostgresDdlGenerator.Generate(
            new CreateRlsPolicyOperation(
                "public",
                "Documents",
                new RlsPolicyDefinition
                {
                    Name = "group_read",
                    IsPermissive = true,
                    Operations = [RlsOperation.Select],
                    UsingLql = $"exists({lql})",
                }
            )
        );

        Assert.Contains("USING (EXISTS (", ddl, StringComparison.Ordinal);
        Assert.Contains(
            "current_setting('rls.current_user_id', true)",
            ddl,
            StringComparison.Ordinal
        );
        Assert.Contains("UserGroupMemberships", ddl, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_CreateRlsPolicy_RawSqlPredicates_EmitVerbatim()
    {
        var ddl = PostgresDdlGenerator.Generate(
            new CreateRlsPolicyOperation(
                "public",
                "Documents",
                new RlsPolicyDefinition
                {
                    Name = "raw_sql",
                    Operations = [RlsOperation.All],
                    UsingLql = "OwnerId = current_user_id()",
                    WithCheckLql = "OwnerId = current_user_id()",
                    UsingSql = "is_member(\"GroupId\")",
                    WithCheckSql = "can_write(\"GroupId\")",
                }
            )
        );

        Assert.Contains("USING (is_member(\"GroupId\"))", ddl, StringComparison.Ordinal);
        Assert.Contains("WITH CHECK (can_write(\"GroupId\"))", ddl, StringComparison.Ordinal);
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
                    new RlsPolicyDefinition { Name = "broken", UsingLql = "" }
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
                    new RlsPolicyDefinition
                    {
                        Name = "broken_check",
                        Operations = [RlsOperation.Insert],
                    }
                )
            )
        );
        Assert.Contains("MIG-E-RLS-EMPTY-CHECK", ex.Message, StringComparison.Ordinal);
    }
}
