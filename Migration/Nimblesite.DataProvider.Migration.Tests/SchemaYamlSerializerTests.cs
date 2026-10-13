// Implements [MIG-TEST-ID-NAME-COLUMNS].
// Implements [CI-TEST-SHARED-COMMAND].

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
        var yaml = VerifySharedContinuation10(
            input0: Fixture_ToYaml_SimpleSchema_ProducesValidYaml_1
        );

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
        AssertTableShapeMatches(usersOriginal, usersRestored);

        var ordersOriginal = schema.Tables.First(t => t.Name == "Orders");
        var ordersRestored = restored.Tables.First(t => t.Name == "Orders");
        AssertTableShapeMatches(ordersOriginal, ordersRestored);
    }

    // Implements [MIG-TEST-YAML-ROUNDTRIP]: a round-tripped table keeps the same
    // column, index and foreign-key counts.
    private static void AssertTableShapeMatches(TableDefinition expected, TableDefinition actual)
    {
        Assert.Equal(expected.Columns.Count, actual.Columns.Count);
        Assert.Equal(expected.Indexes.Count, actual.Indexes.Count);
        Assert.Equal(expected.ForeignKeys.Count, actual.ForeignKeys.Count);
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
        var restored = RoundtripSchema(
            input0: Fixture_ForeignKey_WithActions_SerializesCorrectly_1
        );

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
        var index = RoundtripSingleIndex(input0: Fixture_Index_WithFilter_SerializesCorrectly_1);
        Assert.Equal("idx_active_items", index.Name);
        Assert.True(index.IsUnique);
        Assert.Equal("IsActive = 1", index.Filter);
    }

    [Fact]
    public void ExpressionIndex_SerializesCorrectly()
    {
        // Arrange
        var index = RoundtripSingleIndex(input0: Fixture_ExpressionIndex_SerializesCorrectly_1);
        Assert.Equal("uq_artists_name_ci", index.Name);
        Assert.True(index.IsUnique);
        Assert.Single(index.Expressions);
        Assert.Contains("lower(Name)", index.Expressions);
    }

    [Fact]
    public void CheckConstraint_SerializesCorrectly()
    {
        // Arrange
        var table = Roundtrip(input0: Fixture_CheckConstraint_SerializesCorrectly_1);

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
        var uc = Assert.Single(
            Roundtrip(input0: Fixture_UniqueConstraint_SerializesCorrectly_1).UniqueConstraints
        );
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
        var schema = Data_ToYamlFile_WritesValidYaml_1();

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
        var restored = RoundtripSchema(input0: Fixture_PortableType_EnumType_SerializesCorrectly_1);

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
        var restored = RoundtripSchema(input0: Fixture_DefaultLqlExpression_SerializesCorrectly_1);

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
            global::Tests.Shared.TempSqliteDatabase.DeleteIfExists(path: dbPath);
        }
    }

    [Fact]
    public void ToYaml_OmitsSemanticDefaultValues()
    {
        // Arrange - Schema with all default values
        var yaml = VerifySharedContinuation10(input0: Fixture_ToYaml_OmitsSemanticDefaultValues_1);

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
        var yaml = VerifySharedContinuation10(
            input0: Fixture_VectorType_YamlRoundTrip_PreservesDimensions_1
        );
        var restored = SchemaYamlSerializer.FromYaml(yaml);

        // Assert
        Assert.Contains("Vector(1536)", yaml, StringComparison.Ordinal);
        var col = restored.Tables.Single().Columns.Single(c => c.Name == "Embedding");
        var vec = Assert.IsType<VectorType>(col.Type);
        Assert.Equal(1536, vec.Dimensions);
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(VectorType_PostgresDdl_EmitsPgVectorType_ContextCasesData))]
    public void VectorType_PostgresDdl_EmitsPgVectorType_ContextCases(
        string caseName,
        Func<SchemaDefinition> input0,
        global::System.Func<
            global::Nimblesite.DataProvider.Migration.Core.SchemaOperation,
            global::System.String
        > value0,
        string value3
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: caseName));
        ExerciseOperationCaseMatrix2(input0: input0, value0: value0, value3: value3);
    }

    // Implements [MIG-TEST-YAML-ROUNDTRIP].
    private static TableDefinition RoundtripFirstTable(SchemaDefinition schema) =>
        SchemaYamlSerializer.FromYaml(yaml: SchemaYamlSerializer.ToYaml(schema: schema)).Tables[0];

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static TableDefinition Roundtrip(Func<SchemaDefinition> input0) =>
        VerifyReusableWorkflow13(input0: input0, input1: RoundtripFirstTable);

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static IndexDefinition RoundtripSingleIndex(Func<SchemaDefinition> input0) =>
        Assert.Single(Roundtrip(input0: input0).Indexes);

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static SchemaDefinition RoundtripSchema(Func<SchemaDefinition> input0) =>
        SchemaYamlSerializer.FromYaml(VerifySharedContinuation10(input0: input0));

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static string VerifySharedContinuation10(Func<SchemaDefinition> input0)
    { // Arrange
        var schema = input0();

        // Act
        var yaml = SchemaYamlSerializer.ToYaml(schema);

        return yaml;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static TableDefinition VerifyReusableWorkflow13(
        Func<SchemaDefinition> input0,
        Func<SchemaDefinition, TableDefinition> input1
    )
    { // Arrange
        var schema = input0();

        // Act
        var table = input1(arg: schema);

        return table;
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    private static void ExerciseOperationCaseMatrix2(
        Func<SchemaDefinition> input0,
        global::System.Func<
            global::Nimblesite.DataProvider.Migration.Core.SchemaOperation,
            global::System.String
        > value0,
        string value3
    )
    { // Arrange
        var schema = input0();

        // Act
        var ddl = value0(arg: new CreateTableOperation(schema.Tables[0]));

        // Assert
        Assert.Contains(value3, ddl, StringComparison.Ordinal);
    }
}
