// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SchemaDiffCheckConstraintIssue57Tests
{
    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Calculate_TableCheckConstraintExpressionChanged_DropsAndReadds_1(
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.TableDefinition>? tables,
        global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition current,
        global::System.String name,
        global::System.String expression
    ) =>
        SchemaTestData.Schema(
            name: "desired",
            tables:
            [
                current.Tables[0] with
                {
                    CheckConstraints =
                    [
                        SchemaTestData.CheckConstraint(
                            name: "usage_events_kind_check",
                            expression: "kind IN ('llm_request','llm_input_tokens','llm_output_tokens','machine_seconds')"
                        ),
                    ],
                },
            ]
        );
}
