using static global::Tests.Shared.ResultAssertions;

// Implements [MIG-TEST-SCHEMA-FIXTURES].
namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [MIG-TEST-DIFF-SHARED].

public sealed partial class SchemaDiffSupportTests
{
    [Fact]
    public void Calculate_DeclarativeSupportObjects_OrdersBeforeRlsPolicy()
    {
        var desired = SupportSchema();
        var ops = SchemaDiffAssertions.Diff(
            current: Schema.Define("current").Build(),
            desired: desired
        );
        Assert.Equal(
            [
                typeof(CreateOrAlterRoleOperation),
                typeof(CreateTableOperation),
                typeof(CreateOrReplaceFunctionOperation),
                typeof(GrantPrivilegesOperation),
                typeof(EnableRlsOperation),
                typeof(CreateRlsPolicyOperation),
            ],
            ops.Select(o => o.GetType()).ToArray()
        );
    }

    [Fact]
    public void Calculate_SameSupportObjects_HasNoOperations()
    {
        var schema = SupportSchema();
        var result = SchemaDiff.Calculate(schema, schema);

        Assert.True(result is OperationsResultOk);
        Assert.Empty((Ok(actual: result)).Value);
    }

    [Fact]
    public void Calculate_RemovedFunction_RequiresAllowDestructive()
    {
        var current = SupportSchema();
        var desired = current with { Functions = [] };

        var safe = (Ok(actual: SchemaDiff.Calculate(current, desired))).Value;
        var destructive = (
            Ok(actual: SchemaDiff.Calculate(current, desired, allowDestructive: true))
        ).Value;

        Assert.DoesNotContain(safe, op => op is DropFunctionOperation);
        Assert.Contains(destructive, op => op is DropFunctionOperation);
    }

    [Fact]
    public void Calculate_StaleManagedGrant_AllowDestructive_EmitsRevoke()
    {
        var current = SupportSchema();
        var desired = current with
        {
            Grants = [Dataset_Calculate_StaleManagedGrant_AllowDestructive_EmitsRevoke_1()],
        };

        var result = SchemaDiff.Calculate(current, desired, allowDestructive: true);

        Assert.True(result is OperationsResultOk);
        Assert.Contains((Ok(actual: result)).Value, op => op is RevokePrivilegesOperation);
    }

    [Fact]
    public void Calculate_GrantRunAsAlreadyApplied_HasNoOperations()
    {
        var current = SupportSchema();
        var desired = current with
        {
            Grants = [Dataset_Calculate_GrantRunAsAlreadyApplied_HasNoOperations_1()],
        };

        var result = SchemaDiff.Calculate(current, desired);

        Assert.True(result is OperationsResultOk);
        Assert.Empty((Ok(actual: result)).Value);
    }

    [Fact]
    public void Calculate_MissingGrantRunAs_PreservesRunAsOnOperation()
    {
        var current = SupportSchema() with { Grants = [] };
        var desired = SupportSchema() with
        {
            Grants = [Dataset_Calculate_MissingGrantRunAs_PreservesRunAsOnOperation_1()],
        };

        var grant = Assert.IsType<GrantPrivilegesOperation>(
            Assert.Single((Ok(read: () => SchemaDiff.Calculate(current, desired))).Value)
        );
        Assert.Equal("supabase_admin", grant.Grant.RunAs);
    }
}
