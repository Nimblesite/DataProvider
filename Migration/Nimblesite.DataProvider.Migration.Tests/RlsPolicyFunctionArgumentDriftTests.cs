// Implements [MIG-TEST-SCHEMA-FIXTURES].
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
        SchemaTestData.Schema(
            name: "rls_function_argument_drift",
            tables:
            [
                SchemaTestData.Table(
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
                                operations: [RlsOperation.Select],
                                usingSql: predicate
                            ),
                        ],
                    }
                ),
            ]
        );
}
