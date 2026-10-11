using System.Collections.Frozen;

namespace Nimblesite.DataProvider.Tests;

/// <summary>
/// Tests for source generator types
/// </summary>
public sealed partial class SourceGeneratorTypesTests
{
    [Fact]
    public void ColumnInfo_CanBeCreatedWithBasicProperties()
    {
        // Arrange & Act
        var column = ColumnInfo.Named("Id");

        // Assert
        var namedColumn = (NamedColumn)column;
        Assert.Equal("Id", namedColumn.Name);
        Assert.Null(column.Alias);
        Assert.Null(namedColumn.TableAlias);
    }

    [Fact]
    public void ColumnInfo_CanBeCreatedWithAllProperties()
    {
        // Arrange & Act
        var column = ColumnInfo.Named("Name", "c", "CustomerName");

        // Assert
        var namedColumn = (NamedColumn)column;
        Assert.Equal("Name", namedColumn.Name);
        Assert.Equal("CustomerName", column.Alias);
        Assert.Equal("c", namedColumn.TableAlias);
    }

    [Fact]
    public void ColumnInfo_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var column1 = ColumnInfo.Named("Id", "i", "InvoiceId");
        var column2 = ColumnInfo.Named("Id", "i", "InvoiceId");
        var column3 = ColumnInfo.Named("Name", "i", "InvoiceId");

        // Act & Assert
        Assert.Equal(column1, column2);
        Assert.NotEqual(column1, column3);
    }

    [Fact]
    public void TableInfo_CanBeCreatedWithNameOnly()
    {
        // Arrange & Act
        var table = new TableInfo("Invoice", null);

        // Assert
        Assert.Equal("Invoice", table.Name);
        Assert.Null(table.Alias);
    }

    [Fact]
    public void TableInfo_CanBeCreatedWithAlias()
    {
        // Arrange & Act
        var table = new TableInfo("Invoice", "i");

        // Assert
        Assert.Equal("Invoice", table.Name);
        Assert.Equal("i", table.Alias);
    }

    [Fact]
    public void TableInfo_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var table1 = new TableInfo("Invoice", "i");
        var table2 = new TableInfo("Invoice", "i");
        var table3 = new TableInfo("InvoiceLine", "i");

        // Act & Assert
        Assert.Equal(table1, table2);
        Assert.NotEqual(table1, table3);
    }

    [Fact]
    public void ParameterInfo_CanBeCreatedWithNameOnly()
    {
        // Arrange & Act
        var parameter = new ParameterInfo("id");

        // Assert
        Assert.Equal("id", parameter.Name);
        Assert.Equal("NVARCHAR", parameter.SqlType); // Default
    }

    [Fact]
    public void ParameterInfo_CanBeCreatedWithCustomSqlType()
    {
        // Arrange & Act
        var parameter = new ParameterInfo("id", "INT");

        // Assert
        Assert.Equal("id", parameter.Name);
        Assert.Equal("INT", parameter.SqlType);
    }

    [Fact]
    public void ParameterInfo_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var param1 = new ParameterInfo("id", "INT");
        var param2 = new ParameterInfo("id", "INT");
        var param3 = new ParameterInfo("name", "INT");

        // Act & Assert
        Assert.Equal(param1, param2);
        Assert.NotEqual(param1, param3);
    }

    [Fact]
    public void SqlStatement_CanBeCreatedWithDefaults()
    {
        // Arrange & Act
        var selectStatement = new SelectStatement();

        // Assert
        Assert.NotNull(selectStatement.SelectList);
        Assert.Empty(selectStatement.SelectList);
        Assert.NotNull(selectStatement.Tables);
        Assert.Empty(selectStatement.Tables);
        Assert.NotNull(selectStatement.Parameters);
        Assert.Empty(selectStatement.Parameters);
        Assert.NotNull(selectStatement.JoinGraph);
        Assert.Equal(0, selectStatement.JoinGraph.Count);
        Assert.False(selectStatement.HasJoins);
    }

    [Fact]
    public void SqlStatement_CanBeCreatedWithCustomProperties()
    {
        // Arrange
        var selectList = new List<ColumnInfo>
        {
            ColumnInfo.Named("Id", "i"),
            ColumnInfo.Named("Name", "c", "CustomerName"),
        }.AsReadOnly();

        var tables = new List<TableInfo> { new("Invoice", "i"), new("Customer", "c") }.AsReadOnly();

        var parameters = Dataset_SqlStatement_CanBeCreatedWithCustomProperties_1().AsReadOnly();

        var joinGraph = new JoinGraph();
        joinGraph.Add("Invoice", "Customer", "i.CustomerId = c.Id");

        // Act
        var selectStatement = new SelectStatement
        {
            SelectList = selectList,
            Tables = tables.ToFrozenSet(),
            Parameters = parameters.ToFrozenSet(),
            JoinGraph = joinGraph,
        };

        // Assert
        Assert.Equal(2, selectStatement.SelectList.Count);
        Assert.Equal(2, selectStatement.Tables.Count);
        Assert.Equal(2, selectStatement.Parameters.Count);
        Assert.Equal(1, selectStatement.JoinGraph.Count);
        Assert.True(selectStatement.HasJoins);
    }

    [Fact]
    public void SqlStatement_HasJoins_ReturnsTrueWhenJoinsExist()
    {
        // Arrange
        var joinGraph = new JoinGraph();
        joinGraph.Add("Invoice", "InvoiceLine", "i.Id = l.InvoiceId");

        var selectStatement = new SelectStatement { JoinGraph = joinGraph };

        // Act & Assert
        Assert.True(selectStatement.HasJoins);
    }

    [Fact]
    public void SqlStatement_HasJoins_ReturnsFalseWhenNoJoins()
    {
        // Arrange
        var selectStatement = new SelectStatement();

        // Act & Assert
        Assert.False(selectStatement.HasJoins);
    }

    [Fact]
    public void SqlStatement_Collections_AreImmutable()
    {
        // Arrange
        var selectList = new List<ColumnInfo> { ColumnInfo.Named("Id") }.AsReadOnly();
        var tables = new List<TableInfo> { new("Invoice", null) }.AsReadOnly();
        var parameters = new List<ParameterInfo> { new("id") }.AsReadOnly();

        var selectStatement = new SelectStatement
        {
            SelectList = selectList,
            Tables = tables.ToFrozenSet(),
            Parameters = parameters.ToFrozenSet(),
        };

        // Act & Assert - These should be read-only collections
        Assert.IsAssignableFrom<IReadOnlyList<ColumnInfo>>(selectStatement.SelectList);
        Assert.IsAssignableFrom<IReadOnlySet<TableInfo>>(selectStatement.Tables);
        Assert.IsAssignableFrom<IReadOnlySet<ParameterInfo>>(selectStatement.Parameters);
    }

    [Fact]
    public void JoinRelationship_CanBeCreatedWithBasicProperties()
    {
        // Arrange & Act
        var relationship = new JoinRelationship("Invoice", "InvoiceLine", "i.Id = l.InvoiceId");

        // Assert
        Assert.Equal("Invoice", relationship.LeftTable);
        Assert.Equal("InvoiceLine", relationship.RightTable);
        Assert.Equal("i.Id = l.InvoiceId", relationship.Condition);
        Assert.Equal("INNER", relationship.JoinType); // Default
    }

    [Fact]
    public void JoinRelationship_CanBeCreatedWithCustomJoinType()
    {
        // Arrange & Act
        var relationship = Dataset_JoinRelationship_CanBeCreatedWithCustomJoinType_1();

        // Assert
        Assert.Equal("Invoice", relationship.LeftTable);
        Assert.Equal("Customer", relationship.RightTable);
        Assert.Equal("i.CustomerId = c.Id", relationship.Condition);
        Assert.Equal("LEFT", relationship.JoinType);
    }

    [Fact]
    public void JoinRelationship_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var join1 = new JoinRelationship("Invoice", "InvoiceLine", "i.Id = l.InvoiceId", "INNER");
        var join2 = new JoinRelationship("Invoice", "InvoiceLine", "i.Id = l.InvoiceId", "INNER");
        var join3 = new JoinRelationship("Invoice", "Customer", "i.Id = l.InvoiceId", "INNER");

        // Act & Assert
        Assert.Equal(join1, join2);
        Assert.NotEqual(join1, join3);
    }
}
