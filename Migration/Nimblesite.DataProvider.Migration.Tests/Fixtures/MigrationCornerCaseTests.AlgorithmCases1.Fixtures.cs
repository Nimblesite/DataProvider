// Implements [CI-TEST-SHARED-CASE-MATRIX].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class MigrationCornerCaseTests
{
    public static IEnumerable<object?[]> ColumnName_WithNumbers_Success_CasesData()
    {
        {
            global::System.Func<global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition> input0 =
                Fixture_ColumnName_WithNumbers_Success_1;
            yield return ["ColumnName_WithNumbers_Success", input0];
        }
        {
            global::System.Func<global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition> input0 =
                Fixture_Column_MaximumVarCharLength_Success_1;
            yield return ["Column_MaximumVarCharLength_Success", input0];
        }
        {
            global::System.Func<global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition> input0 =
                Fixture_Decimal_ExtremeScaleAndPrecision_Success_1;
            yield return ["Decimal_ExtremeScaleAndPrecision_Success", input0];
        }
        {
            global::System.Func<global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition> input0 =
                Fixture_AllColumnsNotNull_Success_1;
            yield return ["AllColumnsNotNull_Success", input0];
        }
        {
            global::System.Func<global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition> input0 =
                Fixture_DefaultValue_StringWithQuotes_Success_1;
            yield return ["DefaultValue_StringWithQuotes_Success", input0];
        }
        {
            global::System.Func<global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition> input0 =
                Fixture_DefaultValue_NumericZero_Success_1;
            yield return ["DefaultValue_NumericZero_Success", input0];
        }
        {
            global::System.Func<global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition> input0 =
                Fixture_DefaultValue_BooleanFalse_Success_1;
            yield return ["DefaultValue_BooleanFalse_Success", input0];
        }
        {
            global::System.Func<global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition> input0 =
                Fixture_DefaultValue_CurrentTimestamp_Success_1;
            yield return ["DefaultValue_CurrentTimestamp_Success", input0];
        }
    }
}
