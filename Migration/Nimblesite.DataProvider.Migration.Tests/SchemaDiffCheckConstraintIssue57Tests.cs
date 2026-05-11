namespace Nimblesite.DataProvider.Migration.Tests;

/// <summary>
/// Implements [MIG-CHECK-CONSTRAINT-EXPRESSION-DRIFT] (#57): when a
/// declared CHECK constraint changes its expression — same name, different
/// predicate — the diff must emit operations that drop the live one and
/// re-add the desired one. Closes the silent prod outage where a renamed
/// enum-style CHECK was silently skipped because the diff matched on name
/// only.
/// </summary>
public sealed class SchemaDiffCheckConstraintIssue57Tests
{
    [Fact]
    public void Calculate_TableCheckConstraintExpressionChanged_DropsAndReadds()
    {
        var current = new SchemaDefinition
        {
            Name = "live",
            Tables =
            [
                new TableDefinition
                {
                    Schema = "public",
                    Name = "usage_events",
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
                            Name = "kind",
                            Type = PortableTypes.Text,
                            IsNullable = false,
                        },
                    ],
                    PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
                    CheckConstraints =
                    [
                        new CheckConstraintDefinition
                        {
                            Name = "usage_events_kind_check",
                            Expression =
                                "kind IN ('request','input_tokens','output_tokens','sandbox_seconds')",
                        },
                    ],
                },
            ],
        };
        var desired = new SchemaDefinition
        {
            Name = "desired",
            Tables =
            [
                current.Tables[0] with
                {
                    CheckConstraints =
                    [
                        new CheckConstraintDefinition
                        {
                            Name = "usage_events_kind_check",
                            Expression =
                                "kind IN ('llm_request','llm_input_tokens','llm_output_tokens','machine_seconds')",
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
                op is DropCheckConstraintOperation drop
                && drop.ConstraintName == "usage_events_kind_check"
        );
        Assert.Contains(
            ops,
            op =>
                op is AddCheckConstraintOperation add
                && add.CheckConstraint.Name == "usage_events_kind_check"
                && add.CheckConstraint.Expression.Contains("llm_request", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void Calculate_ColumnCheckConstraintExpressionChanged_DropsAndReadds()
    {
        var current = new SchemaDefinition
        {
            Name = "live",
            Tables =
            [
                new TableDefinition
                {
                    Schema = "public",
                    Name = "usage_events",
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
                            Name = "kind",
                            Type = PortableTypes.Text,
                            IsNullable = false,
                            CheckConstraint =
                                "kind IN ('request','input_tokens','output_tokens','sandbox_seconds')",
                        },
                    ],
                    PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
                },
            ],
        };
        var desired = current with
        {
            Tables =
            [
                current.Tables[0] with
                {
                    Columns =
                    [
                        current.Tables[0].Columns[0],
                        current.Tables[0].Columns[1] with
                        {
                            CheckConstraint =
                                "kind IN ('llm_request','llm_input_tokens','llm_output_tokens','machine_seconds')",
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
        Assert.Contains(ops, op => op is DropCheckConstraintOperation);
        Assert.Contains(
            ops,
            op =>
                op is AddCheckConstraintOperation add
                && add.CheckConstraint.Expression.Contains("llm_request", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void Calculate_CheckConstraintUnchanged_EmitsNoCheckConstraintOps()
    {
        var schema = new SchemaDefinition
        {
            Name = "stable",
            Tables =
            [
                new TableDefinition
                {
                    Schema = "public",
                    Name = "usage_events",
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
                            Name = "kind",
                            Type = PortableTypes.Text,
                            IsNullable = false,
                        },
                    ],
                    PrimaryKey = new PrimaryKeyDefinition { Columns = ["id"] },
                    CheckConstraints =
                    [
                        new CheckConstraintDefinition
                        {
                            Name = "usage_events_kind_check",
                            Expression = "kind IN ('a','b')",
                        },
                    ],
                },
            ],
        };

        var result = SchemaDiff.Calculate(
            current: schema,
            desired: schema,
            allowDestructive: false,
            logger: NullLogger.Instance
        );

        Assert.True(result is OperationsResultOk);
        var ops = ((OperationsResultOk)result).Value;
        Assert.DoesNotContain(ops, op => op is DropCheckConstraintOperation);
        Assert.DoesNotContain(ops, op => op is AddCheckConstraintOperation);
    }
}
