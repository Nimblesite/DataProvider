// Implements [CI-TEST-INDEPENDENT-SCHEMA-DATA].
// Pure immutable fixture definitions; assertions and database workflows remain analysed.

namespace Nimblesite.DataProvider.Tests;

public sealed partial class CodeGenerationExtendedE2ETests
{
    private static DatabaseTable CreateTableWithIdentity() =>
        new()
        {
            Name = "Products",
            Schema = "main",
            Columns = new DatabaseColumn[]
            {
                ColumnTestData.Create(
                    name: "Id",
                    sqlType: "TEXT",
                    cSharpType: "Guid",
                    isPrimaryKey: true
                ),
                ColumnTestData.Create(
                    name: "RowNum",
                    sqlType: "INTEGER",
                    cSharpType: "int",
                    isIdentity: true
                ),
                ColumnTestData.Create(name: "Name", sqlType: "TEXT", cSharpType: "string"),
                ColumnTestData.Create(name: "Price", sqlType: "REAL", cSharpType: "decimal"),
            },
        };
}
