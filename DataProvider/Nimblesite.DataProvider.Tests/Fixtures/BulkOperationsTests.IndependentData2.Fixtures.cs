// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Tests;

public partial class BulkOperationsTests
{
    private static global::System.Collections.Generic.List<global::Nimblesite.DataProvider.Core.DatabaseColumn> Dataset_GenerateBulkUpsertMethod_WithNoPrimaryKey_ReturnsEmpty_1() =>
        new List<DatabaseColumn>
        {
            ColumnTestData.Create(name: "Message", cSharpType: "string", isPrimaryKey: false),
            ColumnTestData.Create(name: "Timestamp", cSharpType: "DateTime", isPrimaryKey: false),
        };

    private static global::System.Collections.Generic.List<global::Nimblesite.DataProvider.Core.DatabaseColumn> Dataset_GenerateBulkUpsertMethod_WithCompositeKey_HandlesCorrectly_1() =>
        new List<DatabaseColumn>
        {
            ColumnTestData.Create(name: "OrderId", cSharpType: "Guid", isPrimaryKey: true),
            ColumnTestData.Create(name: "ProductId", cSharpType: "Guid", isPrimaryKey: true),
            ColumnTestData.Create(name: "Quantity", cSharpType: "int", isNullable: false),
            ColumnTestData.Create(name: "UnitPrice", cSharpType: "decimal", isNullable: false),
        };
}
