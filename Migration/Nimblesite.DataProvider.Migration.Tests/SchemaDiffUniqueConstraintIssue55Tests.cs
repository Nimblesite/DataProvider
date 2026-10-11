// Implements [MIG-TEST-SCHEMA-FIXTURES].
namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [MIG-TEST-DIFF-SHARED].

/// <summary>
/// Implements [MIG-UNIQUE-CONSTRAINT-DIFF] (#55): adding a
/// <c>uniqueConstraints</c> entry to an existing table must produce an
/// <see cref="AddUniqueConstraintOperation"/> regardless of how many other
/// columns, foreign keys, or constraints the table already carries.
/// </summary>
public sealed partial class SchemaDiffUniqueConstraintIssue55Tests
{
    private static IReadOnlyList<ColumnDefinition> AgentConfigColumns() =>
        [
            SchemaTestData.Column(name: "id", type: PortableTypes.Uuid, isNullable: false),
            SchemaTestData.Column(name: "tenant_id", type: PortableTypes.Uuid, isNullable: false),
            SchemaTestData.Column(name: "name", type: PortableTypes.Text, isNullable: false),
        ];

    [Fact]
    public void Calculate_AddingCompositeUniqueConstraintToExistingTable_YieldsAddOperation()
    {
        var current = SchemaTestData.Schema(
            name: "live",
            tables:
            [
                SchemaTestData.Table(
                    schema: "public",
                    name: "agent_configs",
                    columns: AgentConfigColumns(),
                    primaryKey: new PrimaryKeyDefinition { Columns = ["id"] }
                ),
            ]
        );
        var ops =
            global::Nimblesite.DataProvider.Migration.Tests.SchemaDiffCheckConstraintIssue57Tests.VerifySharedStep5(
                input0: Dataset_Calculate_AddingCompositeUniqueConstraintToExistingTable_YieldsAddOperation_1,
                input1: current
            );
        Assert.Contains(
            ops,
            op =>
                op is AddUniqueConstraintOperation add
                && add.UniqueConstraint.Name == "uq_agent_configs_tenant_name"
                && add.UniqueConstraint.Columns.SequenceEqual(["tenant_id", "name"])
        );
    }

    [Fact]
    public void Calculate_ReplayAfterUniqueConstraintApplied_YieldsNoUniqueOperation()
    {
        var unique =
            Dataset_Calculate_ReplayAfterUniqueConstraintApplied_YieldsNoUniqueOperation_1();
        var converged = SchemaTestData.Schema(
            name: "live",
            tables:
            [
                SchemaTestData.Table(
                    schema: "public",
                    name: "agent_configs",
                    columns: AgentConfigColumns(),
                    primaryKey: new PrimaryKeyDefinition { Columns = ["id"] },
                    uniqueConstraints: [unique]
                ),
            ]
        );

        var ops = SchemaDiffAssertions.Diff(
            current: converged,
            desired: converged,
            destructive: false,
            logger: NullLogger.Instance
        );
        Assert.DoesNotContain(ops, op => op is AddUniqueConstraintOperation);
    }
}
