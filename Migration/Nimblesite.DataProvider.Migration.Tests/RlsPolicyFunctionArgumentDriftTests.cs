namespace Nimblesite.DataProvider.Migration.Tests;

public sealed class RlsPolicyFunctionArgumentDriftTests
{
    [Fact]
    public void QualifiedFunctionName_DoesNotHideChangedStringArgument()
    {
        var current = PolicySchema("public.is_member('public.is_member')");
        var desired = PolicySchema("is_member('is_member')");

        var operations = ((OperationsResultOk)SchemaDiff.Calculate(current, desired)).Value;

        Assert.Contains(operations, operation => operation is AlterRlsPolicyOperation);
    }

    private static SchemaDefinition PolicySchema(string predicate) =>
        new()
        {
            Name = "rls_function_argument_drift",
            Tables =
            [
                new TableDefinition
                {
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
                                Operations = [RlsOperation.Select],
                                UsingSql = predicate,
                            },
                        ],
                    },
                },
            ],
        };
}
