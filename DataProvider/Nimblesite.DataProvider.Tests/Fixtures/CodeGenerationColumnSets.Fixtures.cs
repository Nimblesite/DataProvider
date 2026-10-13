// Implements [CI-TEST-INDEPENDENT-SCHEMA-DATA].
// Pure immutable column definitions shared by code-generation test factories;
// assertions and generation workflows remain in the analysed test files.
namespace Nimblesite.DataProvider.Tests;

internal static class CodeGenerationColumnSets
{
    internal static readonly IReadOnlyList<DatabaseColumn> PatientColumns =
    [
        ColumnTestData.Create(name: "Id", sqlType: "TEXT", cSharpType: "Guid", isPrimaryKey: true),
        ColumnTestData.Create(name: "Name", sqlType: "TEXT", cSharpType: "string"),
        ColumnTestData.Create(name: "Age", sqlType: "INTEGER", cSharpType: "int"),
        ColumnTestData.Create(
            name: "Email",
            sqlType: "TEXT",
            cSharpType: "string",
            isNullable: true
        ),
        ColumnTestData.Create(name: "IsActive", sqlType: "INTEGER", cSharpType: "bool"),
    ];

    internal static readonly IReadOnlyList<DatabaseColumn> OrderColumns =
    [
        ColumnTestData.Create(
            name: "OrderId",
            sqlType: "TEXT",
            cSharpType: "Guid",
            isPrimaryKey: true
        ),
        ColumnTestData.Create(name: "CustomerName", sqlType: "TEXT", cSharpType: "string"),
        ColumnTestData.Create(name: "Total", sqlType: "REAL", cSharpType: "decimal"),
    ];

    internal static readonly IReadOnlyList<DatabaseColumn> AllJoinedColumns =
    [
        ColumnTestData.Create(name: "OrderId", sqlType: "TEXT", cSharpType: "Guid"),
        ColumnTestData.Create(name: "CustomerName", sqlType: "TEXT", cSharpType: "string"),
        ColumnTestData.Create(name: "Total", sqlType: "REAL", cSharpType: "decimal"),
        ColumnTestData.Create(name: "LineItemId", sqlType: "TEXT", cSharpType: "Guid"),
        ColumnTestData.Create(name: "ProductName", sqlType: "TEXT", cSharpType: "string"),
        ColumnTestData.Create(name: "Quantity", sqlType: "INTEGER", cSharpType: "int"),
    ];

    internal static readonly List<DatabaseColumn> RepositoryTestColumns =
    [
        ColumnTestData.Create(
            name: "Id",
            cSharpType: "int",
            sqlType: "INTEGER",
            isNullable: false,
            isPrimaryKey: true,
            isIdentity: true,
            isComputed: false
        ),
        ColumnTestData.Create(
            name: "Name",
            cSharpType: "string",
            sqlType: "TEXT",
            isNullable: false,
            isPrimaryKey: false,
            isIdentity: false,
            isComputed: false
        ),
        ColumnTestData.Create(
            name: "Email",
            cSharpType: "string?",
            sqlType: "TEXT",
            isNullable: true,
            isPrimaryKey: false,
            isIdentity: false,
            isComputed: false
        ),
    ];

    internal static readonly IReadOnlyList<DatabaseColumn> GroupColumns =
    [
        ColumnTestData.Create(
            name: "OrderId",
            sqlType: "INTEGER",
            cSharpType: "int",
            isPrimaryKey: true
        ),
        ColumnTestData.Create(name: "OrderDate", sqlType: "TEXT", cSharpType: "string"),
        ColumnTestData.Create(name: "LineId", sqlType: "INTEGER", cSharpType: "int"),
        ColumnTestData.Create(name: "LineQty", sqlType: "INTEGER", cSharpType: "int"),
    ];
}
