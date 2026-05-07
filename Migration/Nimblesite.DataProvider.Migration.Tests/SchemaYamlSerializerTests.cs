using System.Globalization;

namespace Nimblesite.DataProvider.Migration.Tests;

/// <summary>
/// E2E tests for YAML schema serialization and deserialization.
/// </summary>
public sealed class SchemaYamlSerializerTests
{
    [Fact]
    public void ToYaml_SimpleSchema_ProducesValidYaml()
    {
        // Arrange
        var schema = Schema
            .Define("test")
            .Table(
                "Users",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("Email", PortableTypes.VarChar(255), c => c.NotNull())
                        .Column("Name", PortableTypes.Text)
            )
            .Build();

        // Act
        var yaml = SchemaYamlSerializer.ToYaml(schema);

        // Assert
        Assert.NotNull(yaml);
        Assert.Contains("name: test", yaml);
        Assert.Contains("tables:", yaml);
        Assert.Contains("Users", yaml);
        Assert.Contains("Uuid", yaml);
        Assert.Contains("VarChar(255)", yaml);
    }

    [Fact]
    public void FromYaml_ValidYaml_DeserializesCorrectly()
    {
        // Arrange
        var yaml = """
            name: test_schema
            tables:
              - name: Products
                schema: public
                columns:
                  - name: Id
                    type: Uuid
                    isNullable: false
                  - name: Name
                    type: VarChar(200)
                    isNullable: false
                  - name: Price
                    type: Decimal(10,2)
                    isNullable: true
                primaryKey:
                  columns:
                    - Id
            """;

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
        var schema = Schema
            .Define("complex_schema")
            .Table(
                "Users",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("Email", PortableTypes.NVarChar(255), c => c.NotNull())
                        .Column(
                            "CreatedAt",
                            PortableTypes.DateTime(),
                            c => c.NotNull().DefaultLql("now()")
                        )
                        .Column(
                            "IsActive",
                            PortableTypes.Boolean,
                            c => c.NotNull().DefaultLql("true")
                        )
                        .Index("idx_users_email", "Email", unique: true)
            )
            .Table(
                "Orders",
                t =>
                    t.Column("Id", PortableTypes.BigInt, c => c.PrimaryKey().Identity())
                        .Column("UserId", PortableTypes.Uuid, c => c.NotNull())
                        .Column("Total", PortableTypes.Decimal(12, 2), c => c.NotNull())
                        .ForeignKey("UserId", "Users", "Id", ForeignKeyAction.Cascade)
            )
            .Build();

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
        var schema = Schema
            .Define("types_test")
            .Table(
                "AllTypes",
                t =>
                    t.Column("TinyInt", PortableTypes.TinyInt)
                        .Column("SmallInt", PortableTypes.SmallInt)
                        .Column("Int", PortableTypes.Int)
                        .Column("BigInt", PortableTypes.BigInt)
                        .Column("Decimal", PortableTypes.Decimal(18, 4))
                        .Column("Float", PortableTypes.Float)
                        .Column("Double", PortableTypes.Double)
                        .Column("Money", PortableTypes.Money)
                        .Column("Bool", PortableTypes.Boolean)
                        .Column("Char", PortableTypes.Char(10))
                        .Column("VarChar", PortableTypes.VarChar(100))
                        .Column("NChar", PortableTypes.NChar(5))
                        .Column("NVarChar", PortableTypes.NVarChar(500))
                        .Column("NVarCharMax", PortableTypes.NVarCharMax)
                        .Column("Text", PortableTypes.Text)
                        .Column("Binary", PortableTypes.Binary(16))
                        .Column("VarBinary", PortableTypes.VarBinary(256))
                        .Column("VarBinaryMax", PortableTypes.VarBinaryMax)
                        .Column("Blob", PortableTypes.Blob)
                        .Column("Date", PortableTypes.Date)
                        .Column("Time", PortableTypes.Time(3))
                        .Column("DateTime", PortableTypes.DateTime(6))
                        .Column("DateTimeOffset", PortableTypes.DateTimeOffset)
                        .Column("Uuid", PortableTypes.Uuid)
                        .Column("Json", PortableTypes.Json)
                        .Column("Xml", PortableTypes.Xml)
            )
            .Build();

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
        var schema = Schema
            .Define("fk_test")
            .Table("Parent", t => t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Table(
                "Child",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("ParentId", PortableTypes.Uuid, c => c.NotNull())
                        .ForeignKey(
                            "ParentId",
                            "Parent",
                            "Id",
                            onDelete: ForeignKeyAction.Cascade,
                            onUpdate: ForeignKeyAction.SetNull
                        )
            )
            .Build();

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
        var schema = Schema
            .Define("index_test")
            .Table(
                "Items",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("IsActive", PortableTypes.Boolean, c => c.NotNull())
                        .Column("Name", PortableTypes.VarChar(100))
                        .Index("idx_active_items", "Name", unique: true, filter: "IsActive = 1")
            )
            .Build();

        // Act
        var yaml = SchemaYamlSerializer.ToYaml(schema);
        var restored = SchemaYamlSerializer.FromYaml(yaml);

        // Assert
        var table = restored.Tables[0];
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
        var schema = Schema
            .Define("expr_index_test")
            .Table(
                "Artists",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("Name", PortableTypes.VarChar(200), c => c.NotNull())
                        .ExpressionIndex("uq_artists_name_ci", "lower(Name)", unique: true)
            )
            .Build();

        // Act
        var yaml = SchemaYamlSerializer.ToYaml(schema);
        var restored = SchemaYamlSerializer.FromYaml(yaml);

        // Assert
        var table = restored.Tables[0];
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
        var schema = Schema
            .Define("check_test")
            .Table(
                "Products",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column(
                            "Price",
                            PortableTypes.Decimal(10, 2),
                            c => c.NotNull().Check("CK_Products_Price", "Price >= 0")
                        )
                        .Column("Quantity", PortableTypes.Int, c => c.NotNull())
                        .Check("CK_Products_Quantity", "Quantity >= 0")
            )
            .Build();

        // Act
        var yaml = SchemaYamlSerializer.ToYaml(schema);
        var restored = SchemaYamlSerializer.FromYaml(yaml);

        // Assert
        var table = restored.Tables[0];

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
        var schema = Schema
            .Define("unique_test")
            .Table(
                "Users",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("Email", PortableTypes.VarChar(255), c => c.NotNull())
                        .Column("TenantId", PortableTypes.Uuid, c => c.NotNull())
                        .Unique("UQ_Users_Email_Tenant", "Email", "TenantId")
            )
            .Build();

        // Act
        var yaml = SchemaYamlSerializer.ToYaml(schema);
        var restored = SchemaYamlSerializer.FromYaml(yaml);

        // Assert
        var table = restored.Tables[0];
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
        var yaml = """
            name: file_test
            tables:
              - name: TestTable
                columns:
                  - name: Id
                    type: Int
                    isNullable: false
            """;
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
            Assert.Contains("file_write_test", content);
            Assert.Contains("Table1", content);

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
        var schema = Schema
            .Define("enum_test")
            .Table(
                "Items",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column(
                            "Status",
                            PortableTypes.Enum("item_status", "pending", "active", "archived")
                        )
            )
            .Build();

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
        var schema = Schema
            .Define("lql_test")
            .Table(
                "Events",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey().DefaultLql("gen_uuid()"))
                        .Column(
                            "CreatedAt",
                            PortableTypes.DateTime(),
                            c => c.NotNull().DefaultLql("now()")
                        )
                        .Column(
                            "IsActive",
                            PortableTypes.Boolean,
                            c => c.NotNull().DefaultLql("true")
                        )
            )
            .Build();

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
        var yaml = """
            name: integration_test
            tables:
              - name: Users
                schema: public
                columns:
                  - name: Id
                    type: Uuid
                    isNullable: false
                  - name: Email
                    type: VarChar(255)
                    isNullable: false
                  - name: Name
                    type: Text
                    isNullable: true
                primaryKey:
                  columns:
                    - Id
                indexes:
                  - name: idx_users_email
                    columns:
                      - Email
                    isUnique: true
              - name: Orders
                schema: public
                columns:
                  - name: Id
                    type: BigInt
                    isNullable: false
                    isIdentity: true
                  - name: UserId
                    type: Uuid
                    isNullable: false
                  - name: Total
                    type: Decimal(12,2)
                    isNullable: false
                primaryKey:
                  columns:
                    - Id
                foreignKeys:
                  - columns:
                      - UserId
                    referencedTable: Users
                    referencedSchema: public
                    referencedColumns:
                      - Id
                    onDelete: Cascade
                    onUpdate: NoAction
            """;

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
                using var cmd = connection.CreateCommand();
                cmd.CommandText = ddl;
                cmd.ExecuteNonQuery();
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
        var schema = Schema
            .Define("test")
            .Table(
                "Users",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("Name", PortableTypes.Text) // nullable by default
            )
            .Build();

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
        var schema = Schema
            .Define("ai")
            .Table(
                "Documents",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("Embedding", PortableTypes.Vector(1536), c => c.NotNull())
            )
            .Build();

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
        var schema = Schema
            .Define("ai")
            .Table(
                "Documents",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("Embedding", PortableTypes.Vector(768), c => c.NotNull())
            )
            .Build();

        // Act
        var ddl = PostgresDdlGenerator.Generate(new CreateTableOperation(schema.Tables[0]));

        // Assert
        Assert.Contains("\"Embedding\" vector(768) NOT NULL", ddl, StringComparison.Ordinal);
    }

    [Fact]
    public void VectorType_SqliteDdl_FallsBackToBlob()
    {
        // Arrange
        var schema = Schema
            .Define("ai")
            .Table(
                "Documents",
                t =>
                    t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("Embedding", PortableTypes.Vector(768), c => c.NotNull())
            )
            .Build();

        // Act
        var ddl = SqliteDdlGenerator.Generate(new CreateTableOperation(schema.Tables[0]));

        // Assert - SQLite has no native vector; must fall back to BLOB
        Assert.Contains("[Embedding] BLOB NOT NULL", ddl, StringComparison.Ordinal);
    }
}
