// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SchemaDiffCheckConstraintIssue57Tests
{
    private static global::Nimblesite.DataProvider.Migration.Core.CheckConstraintDefinition Dataset_Calculate_TableCheckConstraintExpressionChanged_DropsAndReadds_1() =>
        SchemaTestData.CheckConstraint(
            name: "usage_events_kind_check",
            expression: "kind IN ('request','input_tokens','output_tokens','sandbox_seconds')"
        );

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Dataset_Calculate_TableCheckConstraintExpressionChanged_DropsAndReadds_2(
        global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition current
    ) => Fixture_Calculate_TableCheckConstraintExpressionChanged_DropsAndReadds_1(current: current);

    private static global::Nimblesite.DataProvider.Migration.Core.CheckConstraintDefinition Dataset_Calculate_CheckConstraintUnchanged_EmitsNoCheckConstraintOps_1() =>
        SchemaTestData.CheckConstraint(
            name: "usage_events_kind_check",
            expression: "kind IN ('a','b')"
        );
}
