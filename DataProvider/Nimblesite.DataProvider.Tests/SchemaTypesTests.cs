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

    [Fact]
    public void DatabaseTable_PrimaryKeyColumns_ReturnsCorrectColumns()
    {
        // Arrange
        var columns = Dataset_DatabaseTable_PrimaryKeyColumns_ReturnsCorrectColumns_1()
            .AsReadOnly();

        var table = new DatabaseTable { Columns = columns };

        // Act
        var primaryKeyColumns = table.PrimaryKeyColumns;

        // Assert
        Assert.Equal(2, primaryKeyColumns.Count);
        Assert.Contains(primaryKeyColumns, c => c.Name == "Id");
        Assert.Contains(primaryKeyColumns, c => c.Name == "SecondaryId");
        Assert.DoesNotContain(primaryKeyColumns, c => c.Name == "Name");
        Assert.DoesNotContain(primaryKeyColumns, c => c.Name == "Amount");
    }

    [Fact]
    public void DatabaseTable_InsertableColumns_ExcludesIdentityAndComputed()
    {
        // Arrange
        var columns = Dataset_DatabaseTable_InsertableColumns_ExcludesIdentityAndComputed_1()
            .AsReadOnly();

        var table = new DatabaseTable { Columns = columns };

        // Act
        var insertableColumns = table.InsertableColumns;

        // Assert
        Assert.Equal(2, insertableColumns.Count);
        Assert.Contains(insertableColumns, c => c.Name == "Name");
        Assert.Contains(insertableColumns, c => c.Name == "Amount");
        Assert.DoesNotContain(insertableColumns, c => c.Name == "Id");
        Assert.DoesNotContain(insertableColumns, c => c.Name == "ComputedField");
    }

    [Fact]
    public void DatabaseTable_UpdateableColumns_ExcludesPrimaryKeyIdentityAndComputed()
    {
        // Arrange
        var columns =
            Dataset_DatabaseTable_UpdateableColumns_ExcludesPrimaryKeyIdentityAndComputed_1()
                .AsReadOnly();

        var table = new DatabaseTable { Columns = columns };

        // Act
        var updateableColumns = table.UpdateableColumns;

        // Assert
        Assert.Equal(2, updateableColumns.Count);
        Assert.Contains(updateableColumns, c => c.Name == "Name");
        Assert.Contains(updateableColumns, c => c.Name == "Amount");
        Assert.DoesNotContain(updateableColumns, c => c.Name == "Id");
        Assert.DoesNotContain(updateableColumns, c => c.Name == "CategoryId");
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
        var columns = Dataset_DatabaseTable_ComputedProperties_AreReadOnly_1().AsReadOnly();

        var table = new DatabaseTable { Columns = columns };

        // Act
        var primaryKeys = table.PrimaryKeyColumns;
        var insertable = table.InsertableColumns;
        var updateable = table.UpdateableColumns;

        // Assert - These should be read-only collections
        Assert.IsAssignableFrom<IReadOnlyList<DatabaseColumn>>(primaryKeys);
        Assert.IsAssignableFrom<IReadOnlyList<DatabaseColumn>>(insertable);
        Assert.IsAssignableFrom<IReadOnlyList<DatabaseColumn>>(updateable);
    }
}
