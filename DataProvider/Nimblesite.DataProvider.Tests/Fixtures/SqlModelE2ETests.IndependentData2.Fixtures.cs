// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Tests;

public partial class SqlModelE2ETests
{
    private static global::Nimblesite.DataProvider.Core.DatabaseTable Dataset_DatabaseTableRecord_Properties_WorkCorrectly_1() =>
        new DatabaseTable
        {
            Name = "TestTable",
            Schema = "dbo",
            Columns =
            [
                ColumnTestData.Create(
                    name: "Id",
                    cSharpType: "Guid",
                    sqlType: "TEXT",
                    isPrimaryKey: true
                ),
                ColumnTestData.Create(
                    name: "AutoId",
                    cSharpType: "int",
                    sqlType: "INTEGER",
                    isIdentity: true
                ),
                ColumnTestData.Create(
                    name: "Computed",
                    cSharpType: "string",
                    sqlType: "TEXT",
                    isComputed: true
                ),
                ColumnTestData.Create(name: "Name", cSharpType: "string", sqlType: "TEXT"),
                ColumnTestData.Create(
                    name: "Email",
                    cSharpType: "string",
                    sqlType: "TEXT",
                    isNullable: true,
                    maxLength: 255
                ),
            ],
        };
}
