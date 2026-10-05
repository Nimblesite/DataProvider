namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [MIG-TEST-DIFF-SHARED].

/// <summary>
/// Implements [MIG-UNIQUE-CONSTRAINT-DIFF] (#55): adding a
/// <c>uniqueConstraints</c> entry to an existing table must produce an
/// <see cref="AddUniqueConstraintOperation"/> regardless of how many other
/// columns, foreign keys, or constraints the table already carries.
/// </summary>
public sealed class SchemaDiffUniqueConstraintIssue55Tests
{
    private static IReadOnlyList<ColumnDefinition> AgentConfigColumns() =>
        [
            new ColumnDefinition
            {
                Name = "id",
                Type = PortableTypes.Uuid,
                IsNullable = false,
            },
            new ColumnDefinition
            {
                Name = "tenant_id",
                Type = PortableTypes.Uuid,
                IsNullable = false,
            },
            new ColumnDefinition
            {
                Name = "name",
                Type = PortableTypes.Text,
                IsNullable = false,
            },
        ];

    [Fact]
    public void Calculate_AddingCompositeUniqueConstraintToExistingTable_YieldsAddOperation()
    {
        var current = new SchemaDefinition
        {
            Name = "live",
            Tables =
            [
                new TableDefinition
                {
                    Schema = "public",
                    Name = "agent_configs",
                    Columns = AgentConfigColumns(),
                    PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
                },
            ],
        };
        var desired = new SchemaDefinition
        {
            Name = "desired",
            Tables =
            [
                new TableDefinition
                {
                    Schema = "public",
                    Name = "agent_configs",
                    Columns = current.Tables[0].Columns,
                    PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
                    UniqueConstraints =
                    [
                        new UniqueConstraintDefinition
                        {
                            Name = "uq_agent_configs_tenant_name",
                            Columns = ["tenant_id", "name"],
                        },
                    ],
                },
            ],
        };

        var ops = SchemaDiffAssertions.Diff(
            current: current,
            desired: desired,
            destructive: false,
            logger: NullLogger.Instance
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
        var unique = new UniqueConstraintDefinition
        {
            Name = "uq_agent_configs_tenant_name",
            Columns = ["tenant_id", "name"],
        };
        var converged = new SchemaDefinition
        {
            Name = "live",
            Tables =
            [
                new TableDefinition
                {
                    Schema = "public",
                    Name = "agent_configs",
                    Columns = AgentConfigColumns(),
                    PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
                    UniqueConstraints = [unique],
                },
            ],
        };

        var ops = SchemaDiffAssertions.Diff(
            current: converged,
            desired: converged,
            destructive: false,
            logger: NullLogger.Instance
        );
        Assert.DoesNotContain(ops, op => op is AddUniqueConstraintOperation);
    }
}
