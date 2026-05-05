namespace Nimblesite.DataProvider.Migration.Tests;

public sealed class SchemaDiffSupportTests
{
    [Fact]
    public void Calculate_DeclarativeSupportObjects_OrdersBeforeRlsPolicy()
    {
        var desired = SupportSchema();
        var result = SchemaDiff.Calculate(Schema.Define("current").Build(), desired);

        Assert.True(result is OperationsResultOk);
        var ops = ((OperationsResultOk)result).Value;
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
            Grants =
            [
                new PostgresGrantDefinition
                {
                    Schema = "public",
                    Target = PostgresGrantTarget.AllTablesInSchema,
                    Privileges = ["SELECT"],
                    Roles = ["app_user"],
                },
            ],
        };

        var result = SchemaDiff.Calculate(current, desired, allowDestructive: true);

        Assert.True(result is OperationsResultOk);
        Assert.Contains(((OperationsResultOk)result).Value, op => op is RevokePrivilegesOperation);
    }

    private static SchemaDefinition SupportSchema() =>
        new()
        {
            Name = "support",
            Roles = [new PostgresRoleDefinition { Name = "app_user", GrantTo = ["postgres"] }],
            Tables =
            [
                new TableDefinition
                {
                    Schema = "public",
                    Name = "documents",
                    Columns =
                    [
                        new ColumnDefinition
                        {
                            Name = "id",
                            Type = PortableTypes.Uuid,
                            IsNullable = false,
                        },
                    ],
                    PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
                    RowLevelSecurity = new RlsPolicySetDefinition
                    {
                        Policies =
                        [
                            new RlsPolicyDefinition
                            {
                                Name = "documents_member",
                                Roles = ["app_user"],
                                UsingSql = "is_member()",
                                WithCheckSql = "is_member()",
                            },
                        ],
                    },
                },
            ],
            Functions =
            [
                new PostgresFunctionDefinition
                {
                    Name = "is_member",
                    Returns = "boolean",
                    SecurityDefiner = true,
                    Body = "SELECT true",
                    ExecuteRoles = ["app_user"],
                },
            ],
            Grants =
            [
                new PostgresGrantDefinition
                {
                    Schema = "public",
                    Target = PostgresGrantTarget.AllTablesInSchema,
                    Privileges = ["SELECT", "INSERT"],
                    Roles = ["app_user"],
                },
            ],
        };
}
