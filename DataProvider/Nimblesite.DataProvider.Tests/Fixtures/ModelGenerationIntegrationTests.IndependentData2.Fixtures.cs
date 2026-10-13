// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Tests;

public partial class ModelGenerationIntegrationTests
{
    private static global::System.Collections.Generic.List<global::Nimblesite.DataProvider.Core.DatabaseColumn> Dataset_GenerateRecordType_WithSimpleColumns_GeneratesValidCode_1() =>
        new List<DatabaseColumn>
        {
            ColumnTestData.Create(name: "Id", cSharpType: "int", sqlType: "INTEGER"),
            ColumnTestData.Create(name: "Name", cSharpType: "string", sqlType: "TEXT"),
            ColumnTestData.Create(name: "Email", cSharpType: "string?", sqlType: "TEXT"),
            ColumnTestData.Create(name: "Age", cSharpType: "int?", sqlType: "INTEGER"),
        };

    private static global::System.Collections.Generic.List<global::Nimblesite.DataProvider.Core.DatabaseColumn> Dataset_GenerateRecordType_WithNullTypeName_ReturnsFailure_1() =>
        new List<DatabaseColumn>
        {
            ColumnTestData.Create(name: "Id", cSharpType: "int", sqlType: "INTEGER"),
        };

    private static global::System.Collections.Generic.List<global::Nimblesite.DataProvider.Core.DatabaseColumn> Dataset_GenerateGroupedRecordTypes_WithValidInputs_GeneratesParentAndChild_1() =>
        new List<DatabaseColumn>
        {
            ColumnTestData.Create(name: "Id", cSharpType: "int", sqlType: "INTEGER"),
            ColumnTestData.Create(name: "Name", cSharpType: "string", sqlType: "TEXT"),
            ColumnTestData.Create(name: "Email", cSharpType: "string", sqlType: "TEXT"),
            ColumnTestData.Create(name: "OrderId", cSharpType: "int", sqlType: "INTEGER"),
            ColumnTestData.Create(name: "Amount", cSharpType: "decimal", sqlType: "DECIMAL"),
            ColumnTestData.Create(name: "Status", cSharpType: "string", sqlType: "TEXT"),
        };

    private static global::System.Collections.Generic.List<global::Nimblesite.DataProvider.Core.DatabaseColumn> Dataset_GenerateRawRecordType_WithColumns_GeneratesInternalRecord_1() =>
        new List<DatabaseColumn>
        {
            ColumnTestData.Create(name: "UserId", cSharpType: "int", sqlType: "INTEGER"),
            ColumnTestData.Create(name: "UserName", cSharpType: "string", sqlType: "TEXT"),
            ColumnTestData.Create(name: "OrderCount", cSharpType: "int", sqlType: "INTEGER"),
        };

    private static global::System.Collections.Generic.List<global::Nimblesite.DataProvider.Core.DatabaseColumn> Dataset_GenerateRecordType_WithSpecialCharactersInColumnNames_HandlesCorrectly_1() =>
        new List<DatabaseColumn>
        {
            ColumnTestData.Create(name: "user_id", cSharpType: "int", sqlType: "INTEGER"),
            ColumnTestData.Create(name: "first_name", cSharpType: "string", sqlType: "TEXT"),
            ColumnTestData.Create(name: "is_active", cSharpType: "bool", sqlType: "BOOLEAN"),
            ColumnTestData.Create(name: "created_at", cSharpType: "DateTime", sqlType: "DATETIME"),
        };

    private static global::Nimblesite.DataProvider.Core.DatabaseColumn Dataset_GenerateRecordType_WithLargeNumberOfColumns_GeneratesCorrectly_1(
        global::System.Int32 i
    ) =>
        ColumnTestData.Create(
            name: $"Column{i}",
            cSharpType: i % 2 == 0 ? "string" : "int",
            sqlType: i % 2 == 0 ? "TEXT" : "INTEGER"
        );

    private static global::System.Collections.Generic.List<global::Nimblesite.DataProvider.Core.DatabaseColumn> Dataset_GenerateGroupedRecordTypes_WithNullInputs_ReturnsAppropriateFailures_1() =>
        new List<DatabaseColumn>
        {
            ColumnTestData.Create(name: "Id", cSharpType: "int", sqlType: "INTEGER"),
            ColumnTestData.Create(name: "Name", cSharpType: "string", sqlType: "TEXT"),
        };

    private static global::System.Collections.Generic.List<global::Nimblesite.DataProvider.Core.DatabaseColumn> Dataset_GeneratedCode_CompileabilityTest_ValidCSharp_1() =>
        new List<DatabaseColumn>
        {
            ColumnTestData.Create(name: "Id", cSharpType: "int", sqlType: "INTEGER"),
            ColumnTestData.Create(name: "Name", cSharpType: "string", sqlType: "TEXT"),
            ColumnTestData.Create(name: "CreatedAt", cSharpType: "DateTime?", sqlType: "DATETIME"),
        };
}
