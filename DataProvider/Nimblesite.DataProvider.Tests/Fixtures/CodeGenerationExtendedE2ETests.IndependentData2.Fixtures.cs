// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Tests;

public partial class CodeGenerationExtendedE2ETests
{
    private static global::Nimblesite.DataProvider.Core.TableConfig Dataset_GenerateConfiguredTableOperation_1(
        global::System.String value2,
        global::System.Boolean value4
    ) =>
        new TableConfig
        {
            Name = "Products",
            Schema = value2,
            GenerateInsert = true,
            GenerateUpdate = value4,
        };
}
