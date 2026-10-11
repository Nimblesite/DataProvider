// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Tests;

public partial record QueryBuilderPlatformE2ETests
{
    private static global::Nimblesite.DataProvider.Core.TableConfig Dataset_AssertGeneratedCrudAsync_1() =>
        new TableConfig
        {
            Name = "products",
            GenerateInsert = true,
            GenerateUpdate = true,
            GenerateDelete = true,
        };
}
