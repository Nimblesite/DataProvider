// Implements [MIG-TEST-SCHEMA-FIXTURES].
namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [MIG-TEST-DIFF-SHARED].

/// <summary>
/// Implements [MIG-CHECK-CONSTRAINT-EXPRESSION-DRIFT] (#57): when a
/// declared CHECK constraint changes its expression — same name, different
/// predicate — the diff must emit operations that drop the live one and
/// re-add the desired one. Closes the silent prod outage where a renamed
/// enum-style CHECK was silently skipped because the diff matched on name
/// only.
/// </summary>
public sealed partial class SchemaDiffCheckConstraintIssue57Tests
{
    [Fact]
    public void Calculate_TableCheckConstraintExpressionChanged_DropsAndReadds()
    {
        var current = SchemaTestData.Schema(
            name: "live",
            tables:
            [
                BaseUsageEventsTable() with
                {
                    CheckConstraints =
                    [
                        Dataset_Calculate_TableCheckConstraintExpressionChanged_DropsAndReadds_1(),
                    ],
                },
            ]
        );
        var ops = VerifySharedStep5(
            input0: Dataset_Calculate_TableCheckConstraintExpressionChanged_DropsAndReadds_2,
            input1: current
        );
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
        var current = SchemaTestData.Schema(
            name: "live",
            tables:
            [
                BaseUsageEventsTable() with
                {
                    Columns =
                    [
                        BaseUsageEventsTable().Columns[0],
                        BaseUsageEventsTable().Columns[1] with
                        {
                            CheckConstraint =
                                "kind IN ('request','input_tokens','output_tokens','sandbox_seconds')",
                        },
                    ],
                },
            ]
        );
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

        var ops = SchemaDiffAssertions.Diff(
            current: current,
            desired: desired,
            destructive: false,
            logger: NullLogger.Instance
        );
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
        var schema = SchemaTestData.Schema(
            name: "stable",
            tables:
            [
                BaseUsageEventsTable() with
                {
                    CheckConstraints =
                    [
                        Dataset_Calculate_CheckConstraintUnchanged_EmitsNoCheckConstraintOps_1(),
                    ],
                },
            ]
        );

        var ops = SchemaDiffAssertions.Diff(
            current: schema,
            desired: schema,
            destructive: false,
            logger: NullLogger.Instance
        );
        Assert.DoesNotContain(ops, op => op is DropCheckConstraintOperation);
        Assert.DoesNotContain(ops, op => op is AddCheckConstraintOperation);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static IReadOnlyList<SchemaOperation> VerifySharedStep5(
        Func<SchemaDefinition, SchemaDefinition> input0,
        SchemaDefinition input1
    )
    {
        var desired = input0(input1);

        var ops = SchemaDiffAssertions.Diff(
            current: input1,
            desired: desired,
            destructive: false,
            logger: NullLogger.Instance
        );

        return ops;
    }
}
