// Implements [MIG-TEST-ID-NAME-COLUMNS].
// Implements [CI-TEST-SHARED-COMMAND].
using System.Globalization;
using Tests.Shared;
using static global::Tests.Shared.StringAssertions;

namespace Nimblesite.DataProvider.Migration.Tests;

/// <summary>
/// E2E tests for YAML schema serialization and deserialization.
/// </summary>
public sealed partial class SchemaYamlSerializerTests
{
    [Fact]
    public void ToYaml_SimpleSchema_ProducesValidYaml()
    {
        // Arrange
        var schema = Fixture_ToYaml_SimpleSchema_ProducesValidYaml_1();

        // Act
        var yaml = SchemaYamlSerializer.ToYaml(schema);

        // Assert
        Assert.NotNull(yaml);
        ContainsAll(
            actual: yaml,
            expected: ["name: test", "tables:", "Users", "Uuid", "VarChar(255)"]
        );
    }

    [Fact]
    public void FromYaml_ValidYaml_DeserializesCorrectly()
    {
        // Arrange
        var yaml = Data_FromYaml_ValidYaml_DeserializesCorrectly_1;

        // Act
        var schema = SchemaYamlSerializer.FromYaml(yaml);

        // Assert
        Assert.Equal("test_schema", schema.Name);
        Assert.Single(schema.Tables);

        var table = schema.Tables[0];
        Assert.Equal("Products", table.Name);
        Assert.Equal(3, table.Columns.Count);

        var idCol = table.Columns.First(c => c.Name == "Id");
        Assert.IsType<UuidType>(idCol.Type);
        Assert.False(idCol.IsNullable);

        var nameCol = table.Columns.First(c => c.Name == "Name");
        Assert.IsType<VarCharType>(nameCol.Type);
        Assert.Equal(200, ((VarCharType)nameCol.Type).MaxLength);

        var priceCol = table.Columns.First(c => c.Name == "Price");
        Assert.IsType<DecimalType>(priceCol.Type);
        Assert.Equal(10, ((DecimalType)priceCol.Type).Precision);
        Assert.Equal(2, ((DecimalType)priceCol.Type).Scale);
    }

    [Fact]
    public void RoundTrip_ComplexSchema_PreservesAllData()
    {
        // Arrange
        var schema = Fixture_RoundTrip_ComplexSchema_PreservesAllData_1();

        // Act
        var yaml = SchemaYamlSerializer.ToYaml(schema);
        var restored = SchemaYamlSerializer.FromYaml(yaml);

        // Assert
        Assert.Equal(schema.Name, restored.Name);
        Assert.Equal(schema.Tables.Count, restored.Tables.Count);

        var usersOriginal = schema.Tables.First(t => t.Name == "Users");
        var usersRestored = restored.Tables.First(t => t.Name == "Users");
        Assert.Equal(usersOriginal.Columns.Count, usersRestored.Columns.Count);
        Assert.Equal(usersOriginal.Indexes.Count, usersRestored.Indexes.Count);

        var ordersOriginal = schema.Tables.First(t => t.Name == "Orders");
        var ordersRestored = restored.Tables.First(t => t.Name == "Orders");
        Assert.Equal(ordersOriginal.Columns.Count, ordersRestored.Columns.Count);
        Assert.Equal(ordersOriginal.ForeignKeys.Count, ordersRestored.ForeignKeys.Count);
    }

    [Fact]
    public void PortableType_AllTypes_SerializeCorrectly()
    {
        // Arrange
        var schema = Fixture_PortableType_AllTypes_SerializeCorrectly_1();

        // Act
        var yaml = SchemaYamlSerializer.ToYaml(schema);
        var restored = SchemaYamlSerializer.FromYaml(yaml);

        // Assert - verify each type round-trips correctly
        var original = schema.Tables[0].Columns;
        var rest = restored.Tables[0].Columns;

        Assert.Equal(original.Count, rest.Count);

        for (var i = 0; i < original.Count; i++)
        {
            Assert.Equal(original[i].Type.GetType(), rest[i].Type.GetType());
        }

        // Verify parameterized types
        var decimalCol = rest.First(c => c.Name == "Decimal");
        Assert.Equal(18, ((DecimalType)decimalCol.Type).Precision);
        Assert.Equal(4, ((DecimalType)decimalCol.Type).Scale);

        var charCol = rest.First(c => c.Name == "Char");
        Assert.Equal(10, ((CharType)charCol.Type).Length);

        var varCharCol = rest.First(c => c.Name == "VarChar");
        Assert.Equal(100, ((VarCharType)varCharCol.Type).MaxLength);

        var nvarCharMaxCol = rest.First(c => c.Name == "NVarCharMax");
        Assert.Equal(int.MaxValue, ((NVarCharType)nvarCharMaxCol.Type).MaxLength);
    }

    [Fact]
    public void ForeignKey_WithActions_SerializesCorrectly()
    {
        // Arrange
        var schema = Fixture_ForeignKey_WithActions_SerializesCorrectly_1();

        // Act
        var yaml = SchemaYamlSerializer.ToYaml(schema);
        var restored = SchemaYamlSerializer.FromYaml(yaml);

        // Assert
        var childTable = restored.Tables.First(t => t.Name == "Child");
        Assert.Single(childTable.ForeignKeys);

        var fk = childTable.ForeignKeys[0];
        Assert.Equal("Parent", fk.ReferencedTable);
        Assert.Equal(ForeignKeyAction.Cascade, fk.OnDelete);
        Assert.Equal(ForeignKeyAction.SetNull, fk.OnUpdate);
    }

    [Fact]
    public void Index_WithFilter_SerializesCorrectly()
    {
        // Arrange
        var schema = Fixture_Index_WithFilter_SerializesCorrectly_1();

        // Act
        var table = RoundtripFirstTable(schema: schema);
        Assert.Single(table.Indexes);

        var index = table.Indexes[0];
        Assert.Equal("idx_active_items", index.Name);
        Assert.True(index.IsUnique);
        Assert.Equal("IsActive = 1", index.Filter);
    }

    [Fact]
    public void ExpressionIndex_SerializesCorrectly()
    {
        // Arrange
        var schema = Fixture_ExpressionIndex_SerializesCorrectly_1();

        // Act
        var table = RoundtripFirstTable(schema: schema);
        Assert.Single(table.Indexes);

        var index = table.Indexes[0];
        Assert.Equal("uq_artists_name_ci", index.Name);
        Assert.True(index.IsUnique);
        Assert.Single(index.Expressions);
        Assert.Contains("lower(Name)", index.Expressions);
    }

    [Fact]
    public void CheckConstraint_SerializesCorrectly()
    {
        // Arrange
        var schema = Fixture_CheckConstraint_SerializesCorrectly_1();

        // Act
        var table = RoundtripFirstTable(schema: schema);

        var priceCol = table.Columns.First(c => c.Name == "Price");
        Assert.Equal("Price >= 0", priceCol.CheckConstraint);
        Assert.Equal("CK_Products_Price", priceCol.CheckConstraintName);

        Assert.Single(table.CheckConstraints);
        Assert.Equal("CK_Products_Quantity", table.CheckConstraints[0].Name);
        Assert.Equal("Quantity >= 0", table.CheckConstraints[0].Expression);
    }

    [Fact]
    public void UniqueConstraint_SerializesCorrectly()
    {
        // Arrange
        var schema = Fixture_UniqueConstraint_SerializesCorrectly_1();

        // Act
        var table = RoundtripFirstTable(schema: schema);
        Assert.Single(table.UniqueConstraints);

        var uc = table.UniqueConstraints[0];
        Assert.Equal("UQ_Users_Email_Tenant", uc.Name);
        Assert.Equal(2, uc.Columns.Count);
        Assert.Contains("Email", uc.Columns);
        Assert.Contains("TenantId", uc.Columns);
    }

    [Fact]
    public void FromYamlFile_ValidFile_LoadsSchema()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var yaml = Data_FromYamlFile_ValidFile_LoadsSchema_1;
        File.WriteAllText(tempFile, yaml);

        try
        {
            // Act - read file content and parse
            var yamlContent = File.ReadAllText(tempFile);
            var schema = SchemaYamlSerializer.FromYaml(yamlContent);

            // Assert
            Assert.Equal("file_test", schema.Name);
            Assert.Single(schema.Tables);
            Assert.Equal("TestTable", schema.Tables[0].Name);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ToYamlFile_WritesValidYaml()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var schema = Schema
            .Define("file_write_test")
            .Table("Table1", t => t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Build();

        try
        {
            // Act - serialize to YAML and write to file
            var yaml = SchemaYamlSerializer.ToYaml(schema);
            File.WriteAllText(tempFile, yaml);

            // Assert
            var content = File.ReadAllText(tempFile);
            ContainsAll(actual: content, expected: ["file_write_test", "Table1"]);

            // Verify it can be read back
            var restoredYaml = File.ReadAllText(tempFile);
            var restored = SchemaYamlSerializer.FromYaml(restoredYaml);
            Assert.Equal("file_write_test", restored.Name);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void FromYaml_EmptySchema_ReturnsEmptyDefinition()
    {
        // Arrange
        var yaml = """
            name: empty
            tables: []
            """;

        // Act
        var schema = SchemaYamlSerializer.FromYaml(yaml);

        // Assert
        Assert.Equal("empty", schema.Name);
        Assert.Empty(schema.Tables);
    }

    [Fact]
    public void PortableType_EnumType_SerializesCorrectly()
    {
        // Arrange
        var schema = Fixture_PortableType_EnumType_SerializesCorrectly_1();

        // Act
        var yaml = SchemaYamlSerializer.ToYaml(schema);
        var restored = SchemaYamlSerializer.FromYaml(yaml);

        // Assert
        var statusCol = restored.Tables[0].Columns.First(c => c.Name == "Status");
        Assert.IsType<EnumType>(statusCol.Type);

        var enumType = (EnumType)statusCol.Type;
        Assert.Equal("item_status", enumType.Name);
        Assert.Equal(3, enumType.Values.Count);
        Assert.Contains("pending", enumType.Values);
        Assert.Contains("active", enumType.Values);
        Assert.Contains("archived", enumType.Values);
    }

    [Fact]
    public void DefaultLqlExpression_SerializesCorrectly()
    {
        // Arrange
        var schema = Fixture_DefaultLqlExpression_SerializesCorrectly_1();

        // Act
        var yaml = SchemaYamlSerializer.ToYaml(schema);
        var restored = SchemaYamlSerializer.FromYaml(yaml);

        // Assert
        var idCol = restored.Tables[0].Columns.First(c => c.Name == "Id");
        Assert.Equal("gen_uuid()", idCol.DefaultLqlExpression);

        var createdCol = restored.Tables[0].Columns.First(c => c.Name == "CreatedAt");
        Assert.Equal("now()", createdCol.DefaultLqlExpression);

        var activeCol = restored.Tables[0].Columns.First(c => c.Name == "IsActive");
        Assert.Equal("true", activeCol.DefaultLqlExpression);
    }

    [Fact]
    public void IntegrationTest_YamlToSqlite_CreatesDatabaseSuccessfully()
    {
        // Arrange - Create schema from YAML
        var yaml = Data_IntegrationTest_YamlToSqlite_CreatesDatabaseSuccessfully_1;

        var schema = SchemaYamlSerializer.FromYaml(yaml);

        // Act - Apply to SQLite
        var dbPath = Path.Combine(Path.GetTempPath(), $"schemayaml_{Guid.NewGuid()}.db");
        var connection = new SqliteConnection($"Data Source={dbPath}");
        try
        {
            connection.Open();

            foreach (var table in schema.Tables)
            {
                var ddl = SqliteDdlGenerator.Generate(new CreateTableOperation(table));
                DatabaseCommands.Execute(
                    connection: connection,
                    configure: command => command.CommandText = ddl
                );
            }

            // Assert - Verify tables exist
            using var verifyCmd = connection.CreateCommand();
            verifyCmd.CommandText =
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('Users', 'Orders')";
            var tableCount = Convert.ToInt32(
                verifyCmd.ExecuteScalar(),
                CultureInfo.InvariantCulture
            );
            Assert.Equal(2, tableCount);

            // Verify index exists
            verifyCmd.CommandText =
                "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='idx_users_email'";
            var indexCount = Convert.ToInt32(
                verifyCmd.ExecuteScalar(),
                CultureInfo.InvariantCulture
            );
            Assert.Equal(1, indexCount);
        }
        finally
        {
            connection.Close();
            connection.Dispose();
            if (File.Exists(dbPath))
            {
                try
                {
                    File.Delete(dbPath);
                }
                catch
                {
                    /* File may be locked */
                }
            }
        }
    }

    [Fact]
    public void ToYaml_OmitsSemanticDefaultValues()
    {
        // Arrange - Schema with all default values
        var schema = Fixture_ToYaml_OmitsSemanticDefaultValues_1();

        // Act
        var yaml = SchemaYamlSerializer.ToYaml(schema);

        // Assert - These default values should NOT appear in the YAML
        Assert.DoesNotContain("isNullable: true", yaml);
        Assert.DoesNotContain("identitySeed", yaml);
        Assert.DoesNotContain("identityIncrement", yaml);
        Assert.DoesNotContain("isIdentity: false", yaml);
        Assert.DoesNotContain("isComputedPersisted: false", yaml);
        Assert.DoesNotContain("schema: public", yaml);
    }

    [Fact]
    public void VectorType_YamlRoundTrip_PreservesDimensions()
    {
        // Arrange - schema with a pgvector embeddings column
        var schema = Fixture_VectorType_YamlRoundTrip_PreservesDimensions_1();

        // Act
        var yaml = SchemaYamlSerializer.ToYaml(schema);
        var restored = SchemaYamlSerializer.FromYaml(yaml);

        // Assert
        Assert.Contains("Vector(1536)", yaml, StringComparison.Ordinal);
        var col = restored.Tables.Single().Columns.Single(c => c.Name == "Embedding");
        var vec = Assert.IsType<VectorType>(col.Type);
        Assert.Equal(1536, vec.Dimensions);
    }

    [Fact]
    public void VectorType_PostgresDdl_EmitsPgVectorType()
    {
        // Arrange
        var schema = Fixture_VectorType_PostgresDdl_EmitsPgVectorType_1();

        // Act
        var ddl = PostgresDdlGenerator.Generate(new CreateTableOperation(schema.Tables[0]));

        // Assert
        Assert.Contains("\"Embedding\" vector(768) NOT NULL", ddl, StringComparison.Ordinal);
    }

    [Fact]
    public void VectorType_SqliteDdl_FallsBackToBlob()
    {
        // Arrange
        var schema = Fixture_VectorType_SqliteDdl_FallsBackToBlob_1();

        // Act
        var ddl = SqliteDdlGenerator.Generate(new CreateTableOperation(schema.Tables[0]));

        // Assert - SQLite has no native vector; must fall back to BLOB
        Assert.Contains("[Embedding] BLOB NOT NULL", ddl, StringComparison.Ordinal);
    }

    // Implements [MIG-TEST-YAML-ROUNDTRIP].
    private static TableDefinition RoundtripFirstTable(SchemaDefinition schema) =>
        SchemaYamlSerializer.FromYaml(yaml: SchemaYamlSerializer.ToYaml(schema: schema)).Tables[0];
}
