// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Tests;

public partial class SchemaTypesTests
{
    /// <summary>Independent named scenario inputs and expectations.</summary>
    public static IEnumerable<object?[]> DatabaseTable_PrimaryKeyColumns_ReturnsCorrectColumns_ContextCasesData()
    {
        {
            global::System.Func<global::System.Collections.Generic.List<global::Nimblesite.DataProvider.Core.DatabaseColumn>> input1 =
                Dataset_DatabaseTable_PrimaryKeyColumns_ReturnsCorrectColumns_1;
            yield return
            [
                "DatabaseTable_PrimaryKeyColumns_ReturnsCorrectColumns",
                input1,
                new global::System.Func<
                    global::Nimblesite.DataProvider.Core.DatabaseTable,
                    global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Core.DatabaseColumn>
                >(projection => projection.PrimaryKeyColumns),
                "Id",
                "SecondaryId",
                "Name",
                "Amount",
            ];
        }
        {
            global::System.Func<global::System.Collections.Generic.List<global::Nimblesite.DataProvider.Core.DatabaseColumn>> input1 =
                Dataset_DatabaseTable_InsertableColumns_ExcludesIdentityAndComputed_1;
            yield return
            [
                "DatabaseTable_InsertableColumns_ExcludesIdentityAndComputed",
                input1,
                new global::System.Func<
                    global::Nimblesite.DataProvider.Core.DatabaseTable,
                    global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Core.DatabaseColumn>
                >(projection => projection.InsertableColumns),
                "Name",
                "Amount",
                "Id",
                "ComputedField",
            ];
        }
    }
}
