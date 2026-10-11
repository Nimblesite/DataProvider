using static global::Tests.Shared.ResultAssertions;
using static global::Tests.Shared.StringAssertions;

namespace Nimblesite.DataProvider.Tests;

/// <summary>
/// Tests for bulk insert and upsert code generation
/// </summary>
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

    [Theory]
    // GenerateBulkInsertMethod_WithValidTable_ReturnsSuccess
    [MemberData(nameof(GenerateBulkInsertMethod_WithValidTable_ReturnsSuccess_CasesInlineCases))]
    // GenerateBulkInsertMethod_IncludesErrorHandling

    // GenerateBulkInsertMethod_GeneratesProperTupleType

    public void GenerateBulkInsertMethod_WithValidTable_ReturnsSuccess_Cases(
        string caseValue0,
        string caseValue1,
        string caseValue2,
        string caseValue3
    )
    {
        // Arrange
        var table = CreateTestTable();

        // Act
        var result = DataAccessGenerator.GenerateBulkInsertMethod(table);

        // Assert

        var code = Ok(actual: result).Value;
        ContainsAll(actual: code, expected: [caseValue0, caseValue1, caseValue2, caseValue3]);
    }

    [Fact]
    public void GenerateBulkInsertMethod_WithNullTable_ReturnsError()
    {
        // Act
        var result = DataAccessGenerator.GenerateBulkInsertMethod(null!);

        // Assert

        var error = Expected<StringError>(actual: result).Value;
        Assert.Contains("table cannot be null", error.Message);
    }

    [Fact]
    public void GenerateBulkInsertMethod_WithIdentityColumn_ExcludesIdentityFromInsert()
    {
        // Arrange
        var table = CreateTableWithIdentity();

        // Act
        var result = DataAccessGenerator.GenerateBulkInsertMethod(table);

        // Assert

        var code = Ok(actual: result).Value;
        ContainsAll(actual: code, expected: ["BulkInsertOrdersAsync", "CustomerId", "Total"]);
        Assert.DoesNotContain("Id Id", code);
    }

    [Theory]
    // GenerateBulkInsertMethod_GeneratesBatchHelper
    [MemberData(nameof(GenerateBulkInsertMethod_GeneratesBatchHelper_CasesInlineCases))]
    // GenerateBulkInsertMethod_GeneratesParameterizedQueries

    // GenerateBulkInsertMethod_GeneratesAsyncMethods

    public void GenerateBulkInsertMethod_GeneratesBatchHelper_Cases(
        string caseValue0,
        string caseValue1,
        string caseValue2
    )
    {
        // Arrange
        var table = CreateTestTable();

        // Act
        var result = DataAccessGenerator.GenerateBulkInsertMethod(table);

        // Assert

        var code = Ok(actual: result).Value;
        ContainsAll(actual: code, expected: [caseValue0, caseValue1, caseValue2]);
    }

    [Fact]
    public void GenerateBulkInsertMethod_WithCustomBatchSize_UsesBatchSize()
    {
        // Arrange
        var table = CreateTestTable();

        // Act
        var result = DataAccessGenerator.GenerateBulkInsertMethod(table, batchSize: 500);

        // Assert

        var code = Ok(actual: result).Value;
        Assert.Contains("const int batchSize = 500;", code);
    }

    [Fact]
    public void GenerateBulkUpsertMethod_WithValidTable_ReturnsSuccess()
    {
        // Arrange
        var table = CreateTestTable();

        // Act
        var result = DataAccessGenerator.GenerateBulkUpsertMethod(table);

        // Assert

        var code = Ok(actual: result).Value;
        ContainsAll(
            actual: code,
            expected: ["BulkUpsertProductsAsync", "INSERT OR REPLACE INTO Products"]
        );
    }

    [Fact]
    public void GenerateBulkUpsertMethod_ForPostgres_UsesOnConflict()
    {
        // Arrange
        var table = CreateTestTable();

        // Act
        var result = DataAccessGenerator.GenerateBulkUpsertMethod(
            table,
            databaseType: "Postgres",
            connectionType: "NpgsqlConnection"
        );

        // Assert

        var code = Ok(actual: result).Value;
        ContainsAll(
            actual: code,
            expected: ["ON CONFLICT", "DO UPDATE SET", "EXCLUDED.", "NpgsqlCommand"]
        );
    }

    [Fact]
    public void GenerateBulkUpsertMethod_ForSQLite_UsesReplaceInto()
    {
        // Arrange
        var table = CreateTestTable();

        // Act
        var result = DataAccessGenerator.GenerateBulkUpsertMethod(table, databaseType: "SQLite");

        // Assert

        var code = Ok(actual: result).Value;
        Assert.Contains("INSERT OR REPLACE INTO", code);
        Assert.DoesNotContain("ON CONFLICT", code);
    }

    [Fact]
    public void GenerateBulkUpsertMethod_WithNullTable_ReturnsError()
    {
        // Act
        var result = DataAccessGenerator.GenerateBulkUpsertMethod(null!);

        // Assert

        var error = Expected<StringError>(actual: result).Value;
        Assert.Contains("table cannot be null", error.Message);
    }

    [Fact]
    public void GenerateBulkUpsertMethod_WithNoPrimaryKey_ReturnsEmpty()
    {
        // Arrange
        var table = new DatabaseTable
        {
            Schema = "public",
            Name = "Logs",
            Columns = new List<DatabaseColumn>
            {
                ColumnTestData.Create(name: "Message", cSharpType: "string", isPrimaryKey: false),
                ColumnTestData.Create(
                    name: "Timestamp",
                    cSharpType: "DateTime",
                    isPrimaryKey: false
                ),
            }.AsReadOnly(),
        };

        // Act
        var result = DataAccessGenerator.GenerateBulkUpsertMethod(table);

        // Assert

        var code = Ok(actual: result).Value;
        Assert.Empty(code);
    }

    [Fact]
    public void GenerateBulkUpsertMethod_IncludesErrorHandling()
    {
        // Arrange
        var table = CreateTestTable();

        // Act
        var result = DataAccessGenerator.GenerateBulkUpsertMethod(table);

        // Assert

        var code = Ok(actual: result).Value;
        ContainsAll(
            actual: code,
            expected:
            [
                "try",
                "catch (Exception ex)",
                "Result<int, SqlError>.Error",
                "Bulk upsert failed",
            ]
        );
    }

    [Fact]
    public void GenerateBulkUpsertMethod_IncludesAllColumns()
    {
        // Arrange
        var table = CreateTestTable();

        // Act
        var result = DataAccessGenerator.GenerateBulkUpsertMethod(table, databaseType: "Postgres");

        // Assert

        var code = Ok(actual: result).Value;
        ContainsAll(actual: code, expected: ["Id", "Name", "Price", "Category"]);
    }

    [Fact]
    public void GenerateBulkInsertMethod_HandlesNullableTypes()
    {
        // Arrange
        var table = CreateTestTable();

        // Act
        var result = DataAccessGenerator.GenerateBulkInsertMethod(table);

        // Assert

        var code = Ok(actual: result).Value;
        Assert.Contains("?? (object)DBNull.Value", code);
    }

    [Fact]
    public void GenerateBulkUpsertMethod_Postgres_UpdatesNonKeyColumns()
    {
        // Arrange
        var table = CreateTestTable();

        // Act
        var result = DataAccessGenerator.GenerateBulkUpsertMethod(table, databaseType: "Postgres");

        // Assert

        var code = Ok(actual: result).Value;
        ContainsAll(
            actual: code,
            expected:
            [
                "Name = EXCLUDED.Name",
                "Price = EXCLUDED.Price",
                "Category = EXCLUDED.Category",
            ]
        );
        Assert.DoesNotContain("Id = EXCLUDED.Id", code);
    }

    [Fact]
    public void GenerateBulkInsertMethod_UsesExtensionMethodPattern()
    {
        // Arrange
        var table = CreateTestTable();

        // Act
        var result = DataAccessGenerator.GenerateBulkInsertMethod(table);

        // Assert

        var code = Ok(actual: result).Value;
        Assert.Contains("this IDbTransaction transaction", code);
    }

    [Fact]
    public void GenerateBulkUpsertMethod_WithCompositeKey_HandlesCorrectly()
    {
        // Arrange
        var table = new DatabaseTable
        {
            Schema = "public",
            Name = "OrderItems",
            Columns = new List<DatabaseColumn>
            {
                ColumnTestData.Create(name: "OrderId", cSharpType: "Guid", isPrimaryKey: true),
                ColumnTestData.Create(name: "ProductId", cSharpType: "Guid", isPrimaryKey: true),
                ColumnTestData.Create(name: "Quantity", cSharpType: "int", isNullable: false),
                ColumnTestData.Create(name: "UnitPrice", cSharpType: "decimal", isNullable: false),
            }.AsReadOnly(),
        };

        // Act
        var result = DataAccessGenerator.GenerateBulkUpsertMethod(table, databaseType: "Postgres");

        // Assert

        var code = Ok(actual: result).Value;
        ContainsAll(
            actual: code,
            expected:
            [
                "ON CONFLICT (OrderId, ProductId)",
                "Quantity = EXCLUDED.Quantity",
                "UnitPrice = EXCLUDED.UnitPrice",
            ]
        );
    }
}
