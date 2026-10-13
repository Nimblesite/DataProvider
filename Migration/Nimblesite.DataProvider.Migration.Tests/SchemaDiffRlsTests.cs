// Implements [MIG-TEST-SCHEMA-FIXTURES].
namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [RLS-DIFF] tests from docs/specs/rls-spec.md.

/// <summary>
/// Unit tests for the RLS branch of <see cref="SchemaDiff.Calculate"/>.
/// </summary>
public sealed partial class SchemaDiffRlsTests
{
    // Implements [RLS-DIFF]: unknown live predicates must not bypass security reconciliation.
    [Fact]
    public void Diff_SqliteUnreadablePolicy_DoesNotSilentlyPreserveUnknownPredicates()
    {
        var policy = new RlsPolicyDefinition { Name = "owner" };
        var denied = policy with { UsingSql = "false", WithCheckSql = "false" };
        var current = WithRls(
            rls: new RlsPolicySetDefinition { Policies = [policy] },
            schema: "main"
        );
        var desired = WithRls(
            rls: new RlsPolicySetDefinition { Policies = [denied] },
            schema: "main"
        );
        var ops = SchemaDiffAssertions.Diff(current: current, desired: desired);
        Assert.IsType<ReplaceRlsPolicyOperation>(Assert.Single(ops));
    }

    [Fact]
    public void Diff_NewTableWithRls_EmitsCreateTableThenEnableThenPolicy()
    {
        var current = SchemaTestData.Schema(name: "t", tables: []);
        var desired = WithRls(
            Fixture_Diff_NewTableWithRls_EmitsCreateTableThenEnableThenPolicy_1()
        );

        var ops = SchemaDiffAssertions.Diff(current: current, desired: desired);

        Assert.IsType<CreateTableOperation>(ops[0]);
        Assert.Contains(ops, o => o is EnableRlsOperation);
        Assert.Contains(ops, o => o is CreateRlsPolicyOperation cp && cp.Policy.Name == "owner");

        // Order check: Enable must precede CreatePolicy.
        var enableIdx = ops.ToList().FindIndex(o => o is EnableRlsOperation);
        var createIdx = ops.ToList().FindIndex(o => o is CreateRlsPolicyOperation);
        Assert.True(enableIdx < createIdx);
    }

    [Fact]
    public void Diff_ExistingTableNoRls_DesiredAddsPolicy_EmitsEnableAndCreate()
    {
        var current = WithRls(null);
        var desired = WithRls(
            Fixture_Diff_ExistingTableNoRls_DesiredAddsPolicy_EmitsEnableAndCreate_1()
        );

        var ops = SchemaDiffAssertions.Diff(current: current, desired: desired);

        Assert.Contains(ops, o => o is EnableRlsOperation);
        Assert.Contains(ops, o => o is CreateRlsPolicyOperation);
    }

    [Fact]
    public void Diff_PolicyNameSameInBoth_NoOp()
    {
        var policy = SchemaTestData.RlsPolicy(name: "owner", usingLql: "Id = current_user_id()");
        var current = WithRls(new RlsPolicySetDefinition { Policies = [policy] });
        var desired = WithRls(new RlsPolicySetDefinition { Policies = [policy] });

        var ops = SchemaDiffAssertions.Diff(current: current, desired: desired);

        Assert.DoesNotContain(ops, o => o is EnableRlsOperation);
        Assert.DoesNotContain(ops, o => o is CreateRlsPolicyOperation);
        Assert.DoesNotContain(ops, o => o is DropRlsPolicyOperation);
    }

    [Fact]
    public void Diff_OrphanPolicy_AllowDestructiveFalse_NoDrop()
    {
        var current = WithRls(Dataset_Diff_OrphanPolicy_AllowDestructiveFalse_NoDrop_1());
        var desired = WithRls(new RlsPolicySetDefinition { Policies = [] });

        var ops = SchemaDiffAssertions.Diff(current: current, desired: desired);

        Assert.DoesNotContain(ops, o => o is DropRlsPolicyOperation);
    }

    [Fact]
    public void Diff_OrphanPolicy_AllowDestructiveTrue_EmitsDrop()
    {
        var current = WithRls(Dataset_Diff_OrphanPolicy_AllowDestructiveTrue_EmitsDrop_1());
        var desired = WithRls(new RlsPolicySetDefinition { Policies = [] });

        var ops = SchemaDiffAssertions.Diff(current: current, desired: desired, destructive: true);

        SchemaDiffAssertions.AssertDropsRlsPolicy(ops, "orphan");
    }

    [Fact]
    public void Diff_RlsEnabledInCurrent_DesiredDisabled_AllowDestructive_EmitsDisable()
    {
        var current = WithRls(new RlsPolicySetDefinition { Enabled = true });
        var desired = WithRls(new RlsPolicySetDefinition { Enabled = false });

        var ops = SchemaDiffAssertions.Diff(current: current, desired: desired, destructive: true);

        Assert.Contains(ops, o => o is DisableRlsOperation);
    }

    [Fact]
    public void Diff_RlsEnabledInCurrent_DesiredDisabled_NoAllowDestructive_NoDisable()
    {
        var current = WithRls(new RlsPolicySetDefinition { Enabled = true });
        var desired = WithRls(new RlsPolicySetDefinition { Enabled = false });

        var ops = SchemaDiffAssertions.Diff(current: current, desired: desired);

        Assert.DoesNotContain(ops, o => o is DisableRlsOperation);
    }

    [Fact]
    public void Diff_AddSecondPolicy_OnlyEmitsCreateForNewPolicy()
    {
        var existing = SchemaTestData.RlsPolicy(name: "owner", usingLql: "Id = current_user_id()");
        var current = WithRls(new RlsPolicySetDefinition { Policies = [existing] });
        var desired = WithRls(
            Dataset_Diff_AddSecondPolicy_OnlyEmitsCreateForNewPolicy_1(existing: existing)
        );

        var ops = SchemaDiffAssertions.Diff(current: current, desired: desired);

        var creates = ops.OfType<CreateRlsPolicyOperation>().ToList();
        Assert.Single(creates);
        Assert.Equal("extra", creates[0].Policy.Name);
    }
}
