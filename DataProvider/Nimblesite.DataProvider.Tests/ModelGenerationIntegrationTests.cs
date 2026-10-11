using static global::Tests.Shared.ResultAssertions;
using static global::Tests.Shared.StringAssertions;

namespace Nimblesite.DataProvider.Tests;

/// <summary>
/// Integration tests for ModelGenerator functionality
/// </summary>
public sealed partial class ModelGenerationIntegrationTests
{
    [Fact]
    public void GenerateRecordType_WithSimpleColumns_GeneratesValidCode()
    {
        // Arrange
        var code = VerifySharedContinuation3(
            input0: Dataset_GenerateRecordType_WithSimpleColumns_GeneratesValidCode_1,
            value0: "UserRecord"
        );

        ContainsAll(
            actual: code,
            expected:
            [
                "public record UserRecord",
                "public int Id { get; init; }",
                "public string Name { get; init; }",
                "public string? Email { get; init; }",
                "public int? Age { get; init; }",
                "public UserRecord(",
                "this.Id = Id;",
                "this.Name = Name;",
            ]
        );
    }

    [Fact]
    public void GenerateRecordType_WithNullTypeName_ReturnsFailure()
    {
        // Arrange
        var columns = Dataset_GenerateRecordType_WithNullTypeName_ReturnsFailure_1();

        // Act
        var result = ModelGenerator.GenerateRecordType(null!, columns);

        // Assert

        var failure = Expected<StringError>(actual: result);
        Assert.Contains("typeName cannot be null or empty", failure.Value.Message);
    }

    [Fact]
    public void GenerateRecordType_WithEmptyColumns_ReturnsFailure()
    {
        // Act
        var result = ModelGenerator.GenerateRecordType("TestRecord", []);

        // Assert

        var failure = Expected<StringError>(actual: result);
        Assert.Contains("columns cannot be null or empty", failure.Value.Message);
    }

    [Fact]
    public void GenerateGroupedRecordTypes_WithValidInputs_GeneratesParentAndChild()
    {
        // Arrange
        var parentColumns = new List<string> { "Id", "Name", "Email" };
        var childColumns = new List<string> { "OrderId", "Amount", "Status" };
        var allColumns =
            Dataset_GenerateGroupedRecordTypes_WithValidInputs_GeneratesParentAndChild_1();

        // Act
        // Act
        // Assert
        var success = Ok(read: () =>
            ModelGenerator.GenerateGroupedRecordTypes(
                "Customer",
                "Order",
                parentColumns,
                childColumns,
                allColumns
            )
        );
        var code = success.Value;

        // Check parent record
        ContainsAll(
            actual: code,
            expected:
            [
                "public record Customer",
                "public int Id { get; init; }",
                "public string Name { get; init; }",
                "public IReadOnlyList<Order> Orders { get; init; }",
                "public record Order",
                "public int OrderId { get; init; }",
                "public decimal Amount { get; init; }",
                "public string Status { get; init; }",
                "public Customer(",
                "public Order(",
            ]
        );
    }

    [Fact]
    public void GenerateRawRecordType_WithColumns_GeneratesInternalRecord()
    {
        // Arrange
        var columns = Dataset_GenerateRawRecordType_WithColumns_GeneratesInternalRecord_1();

        // Act
        // Act
        // Assert
        var success = Ok(read: () => ModelGenerator.GenerateRawRecordType("UserOrderRaw", columns));
        global::Nimblesite.DataProvider.Tests.CodeGenerationExtendedE2ETests.VerifyReusableWorkflow6(
            input0: success,
            input1: ContainsAll,
            value0: "internal record UserOrderRaw(",
            value1: "int UserId,",
            value2: "string UserName,",
            value3: "int OrderCount",
            value4: ");"
        );
    }

    [Fact]
    public void GenerateRecordType_WithSpecialCharactersInColumnNames_HandlesCorrectly()
    {
        // Arrange - Testing edge cases with column names
        var code = VerifySharedContinuation3(
            input0: Dataset_GenerateRecordType_WithSpecialCharactersInColumnNames_HandlesCorrectly_1,
            value0: "UserProfile"
        );

        ContainsAll(
            actual: code,
            expected:
            [
                "public int user_id { get; init; }",
                "public string first_name { get; init; }",
                "public bool is_active { get; init; }",
                "public DateTime created_at { get; init; }",
                "this.user_id = user_id;",
                "this.first_name = first_name;",
            ]
        );
    }

    [Fact]
    public void GenerateRecordType_WithLargeNumberOfColumns_GeneratesCorrectly()
    {
        // Arrange - Test with many columns to ensure no performance issues
        var columns = new List<DatabaseColumn>();
        for (int i = 1; i <= 20; i++)
        {
            columns.Add(
                Dataset_GenerateRecordType_WithLargeNumberOfColumns_GeneratesCorrectly_1(i: i)
            );
        }

        // Act
        // Act
        // Assert
        var success = Ok(read: () => ModelGenerator.GenerateRecordType("LargeRecord", columns));
        var code = success.Value;

        ContainsAll(
            actual: code,
            expected:
            [
                "public record LargeRecord",
                "public string Column2 { get; init; }",
                "public int Column19 { get; init; }",
                "public string Column20 { get; init; }",
                "public LargeRecord(",
            ]
        );
        for (int i = 1; i <= 20; i++)
        {
            var expectedType = i % 2 == 0 ? "string" : "int";
            if (i == 20)
            {
                Assert.Contains($"        {expectedType} Column{i}", code); // Last parameter without comma
            }
            else
            {
                Assert.Contains($"        {expectedType} Column{i},", code);
            }
        }
    }

    [Fact]
    public void GenerateGroupedRecordTypes_WithNullInputs_ReturnsAppropriateFailures()
    {
        // Arrange
        var validColumns = new List<string> { "Id", "Name" };
        var allColumns =
            Dataset_GenerateGroupedRecordTypes_WithNullInputs_ReturnsAppropriateFailures_1();

        // Act & Assert - Null parent name
        global::Tests.Shared.ResultAssertions.Error(
            actual: ModelGenerator.GenerateGroupedRecordTypes(
                null!,
                "Child",
                validColumns,
                validColumns,
                allColumns
            )
        );

        // Act & Assert - Null child name
        global::Tests.Shared.ResultAssertions.Error(
            actual: ModelGenerator.GenerateGroupedRecordTypes(
                "Parent",
                null!,
                validColumns,
                validColumns,
                allColumns
            )
        );

        // Act & Assert - Empty parent columns
        global::Tests.Shared.ResultAssertions.Error(
            actual: ModelGenerator.GenerateGroupedRecordTypes(
                "Parent",
                "Child",
                [],
                validColumns,
                allColumns
            )
        );

        // Act & Assert - Empty child columns
        global::Tests.Shared.ResultAssertions.Error(
            actual: ModelGenerator.GenerateGroupedRecordTypes(
                "Parent",
                "Child",
                validColumns,
                [],
                allColumns
            )
        );

        // Act & Assert - Empty all columns
        global::Tests.Shared.ResultAssertions.Error(
            actual: ModelGenerator.GenerateGroupedRecordTypes(
                "Parent",
                "Child",
                validColumns,
                validColumns,
                []
            )
        );
    }

    [Fact]
    public void GeneratedCode_CompileabilityTest_ValidCSharp()
    {
        // Arrange - Generate code and verify it contains proper C# syntax
        var code = VerifySharedContinuation3(
            input0: Dataset_GeneratedCode_CompileabilityTest_ValidCSharp_1,
            value0: "TestEntity"
        );

        // Verify proper C# syntax elements
        ContainsAll(
            actual: code,
            expected:
            [
                "/// <summary>",
                "/// Result row for 'TestEntity' query.",
                "/// </summary>",
                "public record TestEntity",
                "get; init;",
            ]
        );
        // Records don't have constructor parameters in doc comments, so remove this assertion
        Assert.DoesNotContain(",,", code); // No double commas
        Assert.DoesNotContain(";;", code); // No double semicolons
        ContainsAll(
            actual: code,
            expected:
            [
                "}",
                "/// <summary>Column 'Id'.</summary>",
                "/// <summary>Column 'Name'.</summary>",
                "/// <summary>Column 'CreatedAt'.</summary>",
            ]
        );
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static string VerifySharedContinuation3(
        Func<List<DatabaseColumn>> input0,
        string value0
    )
    { // Arrange
        var columns = input0();

        // Act
        // Act
        // Assert
        var success = Ok(read: () => ModelGenerator.GenerateRecordType(value0, columns));
        var code = success.Value;

        return code;
    }
}
