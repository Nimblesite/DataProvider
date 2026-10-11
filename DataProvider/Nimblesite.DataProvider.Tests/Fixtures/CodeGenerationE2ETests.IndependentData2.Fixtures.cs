// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Tests;

public partial class CodeGenerationE2ETests
{
    private static global::Nimblesite.DataProvider.Core.DatabaseColumn Dataset_GenerateQueryMethod_MultipleColumns_MapsAllColumns_1(
        global::System.Int32 i
    ) =>
        ColumnTestData.Create(
            name: $"Column{i}",
            cSharpType: i % 3 == 0 ? "int"
                : i % 3 == 1 ? "string"
                : "bool",
            sqlType: i % 3 == 0 ? "INTEGER"
                : i % 3 == 1 ? "TEXT"
                : "BOOLEAN",
            isNullable: i % 4 == 0
        );
}
