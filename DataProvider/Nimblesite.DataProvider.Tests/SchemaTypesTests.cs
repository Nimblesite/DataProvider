using static global::Tests.Shared.ResultAssertions;
using static global::Tests.Shared.RowAssertions;

namespace Nimblesite.DataProvider.Tests;

/// <summary>
/// Tests for schema types and their functionality
/// </summary>
public sealed partial class SchemaTypesTests
{
    [Fact]
    public void DatabaseColumn_CanBeCreatedWithDefaults()
    {
        // Arrange & Act
        var column = new DatabaseColumn();

        // Assert
        Assert.Equal(string.Empty, column.Name);
        Assert.Equal(string.Empty, column.SqlType);
        Assert.Equal(string.Empty, column.CSharpType);
        Assert.False(column.IsNullable);
        Assert.False(column.IsPrimaryKey);
        Assert.False(column.IsIdentity);
        Assert.False(column.IsComputed);
        Assert.Null(column.MaxLength);
        Assert.Null(column.Precision);
        Assert.Null(column.Scale);
    }

    [Fact]
    public void DatabaseColumn_CanBeCreatedWithAllProperties()
    {
        // Arrange & Act
        var column = Dataset_DatabaseColumn_CanBeCreatedWithAllProperties_1();

        // Assert
        Assert.Equal("CustomerName", column.Name);
        Assert.Equal("NVARCHAR(100)", column.SqlType);
        Assert.Equal("string", column.CSharpType);
        Assert.True(column.IsNullable);
        Assert.False(column.IsPrimaryKey);
        Assert.False(column.IsIdentity);
        Assert.False(column.IsComputed);
        Assert.Equal(100, column.MaxLength);
        Assert.Null(column.Precision);
        Assert.Null(column.Scale);
    }

    [Fact]
    public void DatabaseColumn_CanBeCreatedForDecimalColumn()
    {
        // Arrange & Act
        var column = Dataset_DatabaseColumn_CanBeCreatedForDecimalColumn_1();

        // Assert
        Assert.Equal("TotalAmount", column.Name);
        Assert.Equal("DECIMAL(10,2)", column.SqlType);
        Assert.Equal("decimal", column.CSharpType);
        Assert.False(column.IsNullable);
        Assert.Equal(10, column.Precision);
        Assert.Equal(2, column.Scale);
    }

    [Fact]
    public void DatabaseColumn_CanBeCreatedForIdentityColumn()
    {
        // Arrange & Act
        var column = Dataset_DatabaseColumn_CanBeCreatedForIdentityColumn_1();

        // Assert
        Assert.Equal("Id", column.Name);
        Assert.True(column.IsPrimaryKey);
        Assert.True(column.IsIdentity);
        Assert.False(column.IsComputed);
    }

    [Fact]
    public void DatabaseTable_CanBeCreatedWithDefaults()
    {
        // Arrange & Act
        var table = new DatabaseTable();

        // Assert
        Assert.Equal(string.Empty, table.Schema);
        Assert.Equal(string.Empty, table.Name);
        Assert.NotNull(table.Columns);
        Assert.Empty(table.Columns);
    }

    [Fact]
    public void DatabaseTable_CanBeCreatedWithColumns()
    {
        // Arrange
        var columns = Dataset_DatabaseTable_CanBeCreatedWithColumns_1().AsReadOnly();

        // Act
        var table = Dataset_DatabaseTable_CanBeCreatedWithColumns_2(columns: columns);

        // Assert
        Assert.Equal("dbo", table.Schema);
        Assert.Equal("Invoice", table.Name);
        Assert.Equal(3, table.Columns.Count);
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(DatabaseTable_PrimaryKeyColumns_ReturnsCorrectColumns_ContextCasesData))]
    public void DatabaseTable_PrimaryKeyColumns_ReturnsCorrectColumns_ContextCases(
        string caseName,
        Func<List<DatabaseColumn>> input1,
        global::System.Func<
            global::Nimblesite.DataProvider.Core.DatabaseTable,
            global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Core.DatabaseColumn>
        > value0,
        string value1,
        string value2,
        string value3,
        string value4
    )
    {
        NamedCase(caseName: caseName);
        ExerciseProjectionCaseMatrix1(
            input0: VerifyReusableWorkflow7,
            input1: input1,
            input2: VerifyReusableWorkflow14,
            value0: value0,
            value1: value1,
            value2: value2,
            value3: value3,
            value4: value4
        );
    }

    [Fact]
    public void DatabaseTable_UpdateableColumns_ExcludesPrimaryKeyIdentityAndComputed()
    {
        // Arrange
        var table = VerifyReusableWorkflow7(
            input0: Dataset_DatabaseTable_UpdateableColumns_ExcludesPrimaryKeyIdentityAndComputed_1
        );

        // Act
        var updateableColumns = table.UpdateableColumns;

        // Assert
        VerifyReusableWorkflow14(
            input0: updateableColumns,
            value1: "Name",
            value2: "Amount",
            value3: "Id",
            value4: "CategoryId"
        );
        Assert.DoesNotContain(updateableColumns, c => c.Name == "ComputedField");
    }

    [Fact]
    public void DatabaseTable_EmptyTable_ComputedPropertiesReturnEmpty()
    {
        // Arrange
        var table = new DatabaseTable();

        // Act & Assert
        Assert.Empty(table.PrimaryKeyColumns);
        Assert.Empty(table.InsertableColumns);
        Assert.Empty(table.UpdateableColumns);
    }

    [Fact]
    public void DatabaseColumn_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var column1 = Dataset_DatabaseColumn_RecordEquality_WorksCorrectly_1();
        var column2 = Dataset_DatabaseColumn_RecordEquality_WorksCorrectly_2();
        var column3 = Dataset_DatabaseColumn_RecordEquality_WorksCorrectly_3();

        // Act & Assert
        Assert.Equal(column1, column2);
        Assert.NotEqual(column1, column3);
    }

    [Fact]
    public void DatabaseTable_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var columns = Dataset_DatabaseTable_RecordEquality_WorksCorrectly_1().AsReadOnly();

        var table1 = Dataset_DatabaseTable_RecordEquality_WorksCorrectly_2(columns: columns);
        var table2 = Dataset_DatabaseTable_RecordEquality_WorksCorrectly_3(columns: columns);
        var table3 = Dataset_DatabaseTable_RecordEquality_WorksCorrectly_4(columns: columns);

        // Act & Assert
        Assert.Equal(table1, table2);
        Assert.NotEqual(table1, table3);
    }

    [Fact]
    public void DatabaseTable_ComputedProperties_AreReadOnly()
    {
        // Arrange
        var table = VerifyReusableWorkflow7(
            input0: Dataset_DatabaseTable_ComputedProperties_AreReadOnly_1
        );

        // Act
        var primaryKeys = table.PrimaryKeyColumns;
        var insertable = table.InsertableColumns;
        var updateable = table.UpdateableColumns;

        // Assert - These should be read-only collections
        Assert.IsAssignableFrom<IReadOnlyList<DatabaseColumn>>(primaryKeys);
        Assert.IsAssignableFrom<IReadOnlyList<DatabaseColumn>>(insertable);
        Assert.IsAssignableFrom<IReadOnlyList<DatabaseColumn>>(updateable);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static DatabaseTable VerifyReusableWorkflow7(Func<List<DatabaseColumn>> input0)
    { // Arrange
        var columns = input0().AsReadOnly();

        var table = new DatabaseTable { Columns = columns };

        return table;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyReusableWorkflow14(
        IReadOnlyList<DatabaseColumn> input0,
        string value1,
        string value2,
        string value3,
        string value4
    )
    {
        // Assert
        Assert.Equal(2, input0.Count);
        ContainsAll(rows: input0, projection: c => c.Name, expected: [value1, value2]);
        Assert.DoesNotContain(input0, c => c.Name == value3);
        Assert.DoesNotContain(input0, c => c.Name == value4);
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    private static void ExerciseProjectionCaseMatrix1(
        Func<Func<List<DatabaseColumn>>, DatabaseTable> input0,
        Func<List<DatabaseColumn>> input1,
        Action<IReadOnlyList<DatabaseColumn>, string, string, string, string> input2,
        global::System.Func<
            global::Nimblesite.DataProvider.Core.DatabaseTable,
            global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Core.DatabaseColumn>
        > value0,
        string value1,
        string value2,
        string value3,
        string value4
    )
    { // Arrange
        var table = input0(arg: input1);

        // Act
        var primaryKeyColumns = value0(arg: table);

        // Assert
        input2(arg1: primaryKeyColumns, arg2: value1, arg3: value2, arg4: value3, arg5: value4);
    }
}
