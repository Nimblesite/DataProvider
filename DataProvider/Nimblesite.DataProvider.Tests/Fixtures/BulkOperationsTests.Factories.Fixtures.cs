// Implements [CI-TEST-INDEPENDENT-SCHEMA-DATA].
// Immutable table factories shared by the bulk-operation code-generation tests;
// generation workflows and assertions remain in the analysed test file.
namespace Nimblesite.DataProvider.Tests;

public sealed partial class BulkOperationsTests
{
    private static DatabaseTable CreateTestTable() =>
        new()
        {
            Schema = "public",
            Name = "Products",
            Columns = new List<DatabaseColumn>
            {
                ColumnTestData.Create(
                    name: "Id",
                    cSharpType: "Guid",
                    isPrimaryKey: true,
                    isIdentity: false
                ),
                ColumnTestData.Create(name: "Name", cSharpType: "string", isNullable: false),
                ColumnTestData.Create(name: "Price", cSharpType: "decimal", isNullable: false),
                ColumnTestData.Create(name: "Category", cSharpType: "string", isNullable: true),
            }.AsReadOnly(),
        };

    private static DatabaseTable CreateTableWithIdentity() =>
        new()
        {
            Schema = "public",
            Name = "Orders",
            Columns = new List<DatabaseColumn>
            {
                ColumnTestData.Create(
                    name: "Id",
                    cSharpType: "int",
                    isPrimaryKey: true,
                    isIdentity: true
                ),
                ColumnTestData.Create(name: "CustomerId", cSharpType: "Guid", isNullable: false),
                ColumnTestData.Create(name: "Total", cSharpType: "decimal", isNullable: false),
            }.AsReadOnly(),
        };
}
