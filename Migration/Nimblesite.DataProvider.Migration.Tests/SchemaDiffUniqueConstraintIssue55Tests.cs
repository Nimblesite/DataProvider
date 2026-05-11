namespace Nimblesite.DataProvider.Migration.Tests;

/// <summary>
/// Implements [MIG-UNIQUE-CONSTRAINT-DIFF] (#55): adding a
/// <c>uniqueConstraints</c> entry to an existing table must produce an
/// <see cref="AddUniqueConstraintOperation"/> regardless of how many other
/// columns, foreign keys, or constraints the table already carries.
/// </summary>
public sealed class SchemaDiffUniqueConstraintIssue55Tests
{
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
                    Columns =
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
                    ],
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

        var result = SchemaDiff.Calculate(
            current: current,
            desired: desired,
            allowDestructive: false,
            logger: NullLogger.Instance
        );

        Assert.True(result is OperationsResultOk);
        var ops = ((OperationsResultOk)result).Value;
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
                    Columns =
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
                    ],
                    PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
                    UniqueConstraints = [unique],
                },
            ],
        };

        var result = SchemaDiff.Calculate(
            current: converged,
            desired: converged,
            allowDestructive: false,
            logger: NullLogger.Instance
        );

        Assert.True(result is OperationsResultOk);
        var ops = ((OperationsResultOk)result).Value;
        Assert.DoesNotContain(ops, op => op is AddUniqueConstraintOperation);
    }
}
