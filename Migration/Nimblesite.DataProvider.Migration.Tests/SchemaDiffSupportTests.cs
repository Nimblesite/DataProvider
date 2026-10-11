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
        Assert.Empty(((OperationsResultOk)result).Value);
    }

    [Fact]
    public void Calculate_RemovedFunction_RequiresAllowDestructive()
    {
        var current = SupportSchema();
        var desired = current with { Functions = [] };

        var safe = ((OperationsResultOk)SchemaDiff.Calculate(current, desired)).Value;
        var destructive = (
            (OperationsResultOk)SchemaDiff.Calculate(current, desired, allowDestructive: true)
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
        Assert.Contains(((OperationsResultOk)result).Value, op => op is RevokePrivilegesOperation);
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
        Assert.Empty(((OperationsResultOk)result).Value);
    }

    [Fact]
    public void Calculate_MissingGrantRunAs_PreservesRunAsOnOperation()
    {
        var current = SupportSchema() with { Grants = [] };
        var desired = SupportSchema() with
        {
            Grants = [Dataset_Calculate_MissingGrantRunAs_PreservesRunAsOnOperation_1()],
        };

        var result = SchemaDiff.Calculate(current, desired);

        Assert.True(result is OperationsResultOk);
        var grant = Assert.IsType<GrantPrivilegesOperation>(
            Assert.Single(((OperationsResultOk)result).Value)
        );
        Assert.Equal("supabase_admin", grant.Grant.RunAs);
    }

    private static SchemaDefinition SupportSchema() =>
        SchemaTestData.Schema(
            name: "support",
            roles: [SchemaTestData.PostgresRole(name: "app_user", grantTo: ["postgres"])],
            tables:
            [
                SchemaTestData.Table(
                    schema: "public",
                    name: "documents",
                    columns:
                    [
                        SchemaTestData.Column(
                            name: "id",
                            type: PortableTypes.Uuid,
                            isNullable: false
                        ),
                    ],
                    primaryKey: new PrimaryKeyDefinition { Columns = ["id"] },
                    rowLevelSecurity: new RlsPolicySetDefinition
                    {
                        Policies =
                        [
                            SchemaTestData.RlsPolicy(
                                name: "documents_member",
                                roles: ["app_user"],
                                usingSql: "is_member()",
                                withCheckSql: "is_member()"
                            ),
                        ],
                    }
                ),
            ],
            functions:
            [
                SchemaTestData.PostgresFunction(
                    name: "is_member",
                    returns: "boolean",
                    securityDefiner: true,
                    body: "SELECT true",
                    executeRoles: ["app_user"]
                ),
            ],
            grants:
            [
                SchemaTestData.PostgresGrant(
                    schema: "public",
                    target: PostgresGrantTarget.AllTablesInSchema,
                    privileges: ["SELECT", "INSERT"],
                    roles: ["app_user"]
                ),
            ]
        );
}
