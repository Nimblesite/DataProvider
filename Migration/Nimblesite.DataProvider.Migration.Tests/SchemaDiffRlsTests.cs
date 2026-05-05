namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [RLS-DIFF] tests from docs/specs/rls-spec.md.

/// <summary>
/// Unit tests for the RLS branch of <see cref="SchemaDiff.Calculate"/>.
/// </summary>
public sealed class SchemaDiffRlsTests
{
    private static SchemaDefinition WithRls(RlsPolicySetDefinition? rls) =>
        new()
        {
            Name = "t",
            Tables =
            [
                new TableDefinition
                {
                    Schema = "public",
                    Name = "Documents",
                    Columns = [new ColumnDefinition { Name = "Id", Type = new UuidType() }],
                    PrimaryKey = new PrimaryKeyDefinition { Columns = ["Id"] },
                    RowLevelSecurity = rls,
                },
            ],
        };

    [Fact]
    public void Diff_NewTableWithRls_EmitsCreateTableThenEnableThenPolicy()
    {
        var current = new SchemaDefinition { Name = "t", Tables = [] };
        var desired = WithRls(
            new RlsPolicySetDefinition
            {
                Policies =
                [
                    new RlsPolicyDefinition { Name = "owner", UsingLql = "Id = current_user_id()" },
                ],
            }
        );

        var ops = ((OperationsResultOk)SchemaDiff.Calculate(current, desired)).Value;

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
            new RlsPolicySetDefinition
            {
                Policies =
                [
                    new RlsPolicyDefinition { Name = "owner", UsingLql = "Id = current_user_id()" },
                ],
            }
        );

        var ops = ((OperationsResultOk)SchemaDiff.Calculate(current, desired)).Value;

        Assert.Contains(ops, o => o is EnableRlsOperation);
        Assert.Contains(ops, o => o is CreateRlsPolicyOperation);
    }

    [Fact]
    public void Diff_PolicyNameSameInBoth_NoOp()
    {
        var policy = new RlsPolicyDefinition
        {
            Name = "owner",
            UsingLql = "Id = current_user_id()",
        };
        var current = WithRls(new RlsPolicySetDefinition { Policies = [policy] });
        var desired = WithRls(new RlsPolicySetDefinition { Policies = [policy] });

        var ops = ((OperationsResultOk)SchemaDiff.Calculate(current, desired)).Value;

        Assert.DoesNotContain(ops, o => o is EnableRlsOperation);
        Assert.DoesNotContain(ops, o => o is CreateRlsPolicyOperation);
        Assert.DoesNotContain(ops, o => o is DropRlsPolicyOperation);
    }

    [Fact]
    public void Diff_OrphanPolicy_AllowDestructiveFalse_NoDrop()
    {
        var current = WithRls(
            new RlsPolicySetDefinition
            {
                Policies = [new RlsPolicyDefinition { Name = "orphan", UsingLql = "true" }],
            }
        );
        var desired = WithRls(new RlsPolicySetDefinition { Policies = [] });

        var ops = ((OperationsResultOk)SchemaDiff.Calculate(current, desired)).Value;

        Assert.DoesNotContain(ops, o => o is DropRlsPolicyOperation);
    }

    [Fact]
    public void Diff_OrphanPolicy_AllowDestructiveTrue_EmitsDrop()
    {
        var current = WithRls(
            new RlsPolicySetDefinition
            {
                Policies = [new RlsPolicyDefinition { Name = "orphan", UsingLql = "true" }],
            }
        );
        var desired = WithRls(new RlsPolicySetDefinition { Policies = [] });

        var ops = (
            (OperationsResultOk)SchemaDiff.Calculate(current, desired, allowDestructive: true)
        ).Value;

        Assert.Contains(ops, o => o is DropRlsPolicyOperation drop && drop.PolicyName == "orphan");
    }

    [Fact]
    public void Diff_RlsEnabledInCurrent_DesiredDisabled_AllowDestructive_EmitsDisable()
    {
        var current = WithRls(new RlsPolicySetDefinition { Enabled = true });
        var desired = WithRls(new RlsPolicySetDefinition { Enabled = false });

        var ops = (
            (OperationsResultOk)SchemaDiff.Calculate(current, desired, allowDestructive: true)
        ).Value;

        Assert.Contains(ops, o => o is DisableRlsOperation);
    }

    [Fact]
    public void Diff_RlsEnabledInCurrent_DesiredDisabled_NoAllowDestructive_NoDisable()
    {
        var current = WithRls(new RlsPolicySetDefinition { Enabled = true });
        var desired = WithRls(new RlsPolicySetDefinition { Enabled = false });

        var ops = ((OperationsResultOk)SchemaDiff.Calculate(current, desired)).Value;

        Assert.DoesNotContain(ops, o => o is DisableRlsOperation);
    }

    [Fact]
    public void Diff_AddSecondPolicy_OnlyEmitsCreateForNewPolicy()
    {
        var existing = new RlsPolicyDefinition
        {
            Name = "owner",
            UsingLql = "Id = current_user_id()",
        };
        var current = WithRls(new RlsPolicySetDefinition { Policies = [existing] });
        var desired = WithRls(
            new RlsPolicySetDefinition
            {
                Policies =
                [
                    existing,
                    new RlsPolicyDefinition { Name = "extra", UsingLql = "true" },
                ],
            }
        );

        var ops = ((OperationsResultOk)SchemaDiff.Calculate(current, desired)).Value;

        var creates = ops.OfType<CreateRlsPolicyOperation>().ToList();
        Assert.Single(creates);
        Assert.Equal("extra", creates[0].Policy.Name);
    }
}
