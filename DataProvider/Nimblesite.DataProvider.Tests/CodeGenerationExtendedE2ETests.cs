using Nimblesite.Sql.Model;
using Outcome;

namespace Nimblesite.DataProvider.Tests;

public sealed partial class CodeGenerationExtendedE2ETests
{
    private static readonly IReadOnlyList<DatabaseColumn> OrderColumns =
    [
        new()
        {
            Name = "OrderId",
            SqlType = "TEXT",
            CSharpType = "Guid",
            IsPrimaryKey = true,
        },
        new()
        {
            Name = "CustomerName",
            SqlType = "TEXT",
            CSharpType = "string",
        },
        new()
        {
            Name = "Total",
            SqlType = "REAL",
            CSharpType = "decimal",
        },
    ];

    private static readonly IReadOnlyList<DatabaseColumn> AllJoinedColumns =
    [
        new()
        {
            Name = "OrderId",
            SqlType = "TEXT",
            CSharpType = "Guid",
        },
        new()
        {
            Name = "CustomerName",
            SqlType = "TEXT",
            CSharpType = "string",
        },
        new()
        {
            Name = "Total",
            SqlType = "REAL",
            CSharpType = "decimal",
        },
        new()
        {
            Name = "LineItemId",
            SqlType = "TEXT",
            CSharpType = "Guid",
        },
        new()
        {
            Name = "ProductName",
            SqlType = "TEXT",
            CSharpType = "string",
        },
        new()
        {
            Name = "Quantity",
            SqlType = "INTEGER",
            CSharpType = "int",
        },
    ];

    private static GroupingConfig CreateOrderGroupingConfig() =>
        new(
            QueryName: "OrdersWithItems",
            GroupingStrategy: "OneToMany",
            ParentEntity: new EntityConfig(
                Name: "Order",
                KeyColumns: ["OrderId"],
                Columns: ["OrderId", "CustomerName", "Total"]
            ),
            ChildEntity: new EntityConfig(
                Name: "LineItem",
                KeyColumns: ["LineItemId"],
                Columns: ["LineItemId", "ProductName", "Quantity"]
            )
        );

    private static DatabaseTable CreateTableWithIdentity() =>
        new()
        {
            Name = "Products",
            Schema = "main",
            Columns = new DatabaseColumn[]
            {
                new()
                {
                    Name = "Id",
                    SqlType = "TEXT",
                    CSharpType = "Guid",
                    IsPrimaryKey = true,
                },
                new()
                {
                    Name = "RowNum",
                    SqlType = "INTEGER",
                    CSharpType = "int",
                    IsIdentity = true,
                },
                new()
                {
                    Name = "Name",
                    SqlType = "TEXT",
                    CSharpType = "string",
                },
                new()
                {
                    Name = "Price",
                    SqlType = "REAL",
                    CSharpType = "decimal",
                },
            },
        };

    private static Task<Result<IReadOnlyList<DatabaseColumn>, SqlError>> MockGetColumnMetadata(
        string connectionString,
        string sql,
        IEnumerable<ParameterInfo> parameters
    ) =>
        Task.FromResult<Result<IReadOnlyList<DatabaseColumn>, SqlError>>(
            new Result<IReadOnlyList<DatabaseColumn>, SqlError>.Ok<
                IReadOnlyList<DatabaseColumn>,
                SqlError
            >(AllJoinedColumns)
        );

    private static CodeGenerationConfig CreateConfig() =>
        new(getColumnMetadata: MockGetColumnMetadata);

    [Fact]
    public void GenerateGroupedQueryMethod_ViaConfig_ProducesParentChildCode()
    {
        var result = CreateConfig()
            .GenerateGroupedQueryMethod(
                "OrderQueries",
                "GetOrdersWithItems",
                "SELECT o.OrderId, o.CustomerName, o.Total, li.LineItemId, li.ProductName, li.Quantity FROM Orders o JOIN LineItems li ON o.OrderId = li.OrderId",
                Array.Empty<ParameterInfo>(),
                AllJoinedColumns,
                CreateOrderGroupingConfig(),
                "SqliteConnection"
            );

        if (result is not StringOk ok)
        {
            Assert.Fail("Expected StringOk");
            return;
        }
        var code = ok.Value;

        Assert.Contains("public static partial class OrderQueries", code);
        Assert.Contains("GetOrdersWithItemsAsync", code);
        Assert.Contains("SqliteConnection", code);
        Assert.Contains("SELECT o.OrderId", code);
        Assert.Contains("GroupResults", code);
        Assert.Contains("Order", code);
        Assert.Contains("LineItem", code);
    }

    [Fact]
    public void GenerateGroupedQueryMethod_WithParameters_IncludesParameterHandling()
    {
        var result = CreateConfig()
            .GenerateGroupedQueryMethod(
                "OrderQueries",
                "GetOrdersByCustomer",
                "SELECT o.OrderId FROM Orders o JOIN LineItems li ON o.OrderId = li.OrderId WHERE o.CustomerId = @customerId",
                new List<ParameterInfo> { new(Name: "customerId", SqlType: "TEXT") },
                AllJoinedColumns,
                CreateOrderGroupingConfig(),
                "SqliteConnection"
            );

        if (result is not StringOk ok)
        {
            Assert.Fail("Expected StringOk");
            return;
        }
        var code = ok.Value;

        Assert.Contains("customerId", code);
        Assert.Contains("@customerId", code);
        Assert.Contains("AddWithValue", code);
        Assert.Contains("GetOrdersByCustomerAsync", code);
    }

    [Fact]
    public void GenerateGroupedQueryMethod_EmptyClassName_ReturnsError()
    {
        var result = CreateConfig()
            .GenerateGroupedQueryMethod(
                "",
                "Test",
                "SELECT 1",
                Array.Empty<ParameterInfo>(),
                AllJoinedColumns,
                CreateOrderGroupingConfig(),
                "SqliteConnection"
            );

        if (result is not StringError err)
        {
            Assert.Fail("Expected StringError");
            return;
        }
        Assert.Contains("className", err.Value.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenerateGroupedQueryMethod_EmptyColumns_ReturnsError()
    {
        var result = CreateConfig()
            .GenerateGroupedQueryMethod(
                "TestClass",
                "TestMethod",
                "SELECT 1",
                Array.Empty<ParameterInfo>(),
                Array.Empty<DatabaseColumn>(),
                CreateOrderGroupingConfig(),
                "SqliteConnection"
            );

        Assert.True(result is StringError);
    }

    [Fact]
    public void GenerateTableOperations_WithInsertAndUpdate_ProducesAllMethods()
    {
        var generator = new DefaultTableOperationGenerator(connectionType: "SqliteConnection");
        var tableConfig = new TableConfig
        {
            Name = "Products",
            Schema = "main",
            GenerateInsert = true,
            GenerateUpdate = true,
        };

        var result = generator.GenerateTableOperations(
            table: CreateTableWithIdentity(),
            config: tableConfig
        );

        if (result is not StringOk ok)
        {
            Assert.Fail("Expected StringOk");
            return;
        }
        var code = ok.Value;

        Assert.Contains("ProductsExtensions", code);
        Assert.Contains("INSERT", code, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UPDATE", code, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("namespace Generated", code);
        Assert.Contains("Microsoft.Data.Sqlite", code);
    }

    [Fact]
    public void GenerateTableOperations_NullTable_ReturnsError()
    {
        var generator = new DefaultTableOperationGenerator();
        var tableConfig = new TableConfig { Name = "Test", GenerateInsert = true };

        var result = generator.GenerateTableOperations(table: null!, config: tableConfig);

        if (result is not StringError err)
        {
            Assert.Fail("Expected StringError");
            return;
        }
        Assert.Contains("table", err.Value.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenerateTableOperations_NullConfig_ReturnsError()
    {
        var result = new DefaultTableOperationGenerator().GenerateTableOperations(
            table: CreateTableWithIdentity(),
            config: null!
        );

        if (result is not StringError err)
        {
            Assert.Fail("Expected StringError");
            return;
        }
        Assert.Contains("config", err.Value.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenerateInsertMethod_ExcludesIdentityColumns()
    {
        var result = new DefaultTableOperationGenerator(
            connectionType: "SqliteConnection"
        ).GenerateInsertMethod(table: CreateTableWithIdentity());

        if (result is not StringOk ok)
        {
            Assert.Fail("Expected StringOk");
            return;
        }
        var code = ok.Value;

        Assert.Contains("INSERT", code, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Products", code);
        Assert.Contains("Name", code);
        Assert.Contains("Price", code);
        Assert.DoesNotContain("RowNum", code);
    }

    [Fact]
    public void GenerateUpdateMethod_UsesPrimaryKeyInWhere()
    {
        var result = new DefaultTableOperationGenerator(
            connectionType: "SqliteConnection"
        ).GenerateUpdateMethod(table: CreateTableWithIdentity());

        if (result is not StringOk ok)
        {
            Assert.Fail("Expected StringOk");
            return;
        }
        var code = ok.Value;

        Assert.Contains("UPDATE", code, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Products", code);
        Assert.Contains("WHERE", code, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Id", code);
    }

    [Fact]
    public void GenerateModelType_ProducesRecordWithProperties()
    {
        var result = new DefaultCodeTemplate().GenerateModelType(
            typeName: "PatientRecord",
            columns: OrderColumns
        );

        if (result is not StringOk ok)
        {
            Assert.Fail("Expected StringOk");
            return;
        }
        var code = ok.Value;

        Assert.Contains("PatientRecord", code);
        Assert.Contains("OrderId", code);
        Assert.Contains("CustomerName", code);
        Assert.Contains("Total", code);
    }

    [Fact]
    public void GenerateModelType_EmptyTypeName_ReturnsError()
    {
        var result = new DefaultCodeTemplate().GenerateModelType(
            typeName: "",
            columns: OrderColumns
        );

        Assert.True(result is StringError);
    }

    [Fact]
    public void GenerateDataAccessMethod_ProducesExtensionMethod()
    {
        var result = new DefaultCodeTemplate().GenerateDataAccessMethod(
            methodName: "GetHighValueOrders",
            returnTypeName: "OrderRecord",
            sql: "SELECT OrderId, CustomerName, Total FROM Orders WHERE Total > @minTotal",
            parameters: new List<ParameterInfo> { new(Name: "minTotal", SqlType: "REAL") },
            columns: OrderColumns
        );

        if (result is not StringOk ok)
        {
            Assert.Fail("Expected StringOk");
            return;
        }
        var code = ok.Value;

        Assert.Contains("GetHighValueOrdersExtensions", code);
        Assert.Contains("GetHighValueOrders", code);
        Assert.Contains("OrderRecord", code);
        Assert.Contains("minTotal", code);
        Assert.Contains("SqliteConnection", code);
    }

    [Fact]
    public void GenerateSourceFile_CombinesModelAndDataAccess()
    {
        var result = new DefaultCodeTemplate().GenerateSourceFile(
            namespaceName: "MyApp.Generated",
            modelCode: "public sealed record OrderRecord(Guid OrderId, string CustomerName);",
            dataAccessCode: "public static partial class OrderQueries { }"
        );

        if (result is not StringOk ok)
        {
            Assert.Fail("Expected StringOk");
            return;
        }
        var code = ok.Value;

        Assert.Contains("namespace MyApp.Generated;", code);
        Assert.Contains("using System;", code);
        Assert.Contains("using Microsoft.Data.Sqlite;", code);
        Assert.Contains("using Outcome;", code);
        Assert.Contains("OrderRecord", code);
        Assert.Contains("OrderQueries", code);
    }

    [Fact]
    public void GenerateSourceFile_EmptyNamespace_ReturnsError() =>
        Assert.True(
            new DefaultCodeTemplate().GenerateSourceFile(
                namespaceName: "",
                modelCode: "record Test();",
                dataAccessCode: "class Foo { }"
            ) is StringError
        );

    [Fact]
    public void GenerateSourceFile_BothCodesEmpty_ReturnsError() =>
        Assert.True(
            new DefaultCodeTemplate().GenerateSourceFile(
                namespaceName: "MyApp",
                modelCode: "",
                dataAccessCode: ""
            ) is StringError
        );

    [Fact]
    public void GenerateSourceFile_OnlyModelCode_Succeeds()
    {
        var result = new DefaultCodeTemplate().GenerateSourceFile(
            namespaceName: "MyApp",
            modelCode: "public sealed record Widget(string Name);",
            dataAccessCode: ""
        );

        if (result is not StringOk ok)
        {
            Assert.Fail("Expected StringOk");
            return;
        }
        Assert.Contains("Widget", ok.Value);
        Assert.Contains("namespace MyApp;", ok.Value);
    }

    [Fact]
    public void GenerateGroupedModels_ProducesParentAndChildTypes()
    {
        var result = new DefaultCodeTemplate().GenerateGroupedModels(
            groupingConfig: CreateOrderGroupingConfig(),
            columns: AllJoinedColumns
        );

        if (result is not StringOk ok)
        {
            Assert.Fail("Expected StringOk");
            return;
        }
        Assert.Contains("Order", ok.Value);
        Assert.Contains("LineItem", ok.Value);
    }

    [Fact]
    public void GenerateGroupedModels_NullConfig_ReturnsError() =>
        Assert.True(
            new DefaultCodeTemplate().GenerateGroupedModels(
                groupingConfig: null!,
                columns: AllJoinedColumns
            ) is StringError
        );

    [Fact]
    public void GenerateGroupedModels_EmptyColumns_ReturnsError() =>
        Assert.True(
            new DefaultCodeTemplate().GenerateGroupedModels(
                groupingConfig: CreateOrderGroupingConfig(),
                columns: Array.Empty<DatabaseColumn>()
            ) is StringError
        );

    [Fact]
    public void GenerateTableOperations_InsertOnly_OmitsUpdate()
    {
        var generator = new DefaultTableOperationGenerator(connectionType: "SqliteConnection");
        var tableConfig = new TableConfig
        {
            Name = "Products",
            Schema = "main",
            GenerateInsert = true,
            GenerateUpdate = false,
        };

        var result = generator.GenerateTableOperations(
            table: CreateTableWithIdentity(),
            config: tableConfig
        );

        if (result is not StringOk ok)
        {
            Assert.Fail("Expected StringOk");
            return;
        }
        Assert.Contains("INSERT", ok.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ProductsExtensions", ok.Value);
    }

    [Fact]
    public void DefaultTableOperationGenerator_SqlServerConnectionType_UsesCorrectNamespace()
    {
        var generator = new DefaultTableOperationGenerator(connectionType: "SqlConnection");
        var tableConfig = new TableConfig
        {
            Name = "Products",
            Schema = "dbo",
            GenerateInsert = true,
            GenerateUpdate = true,
        };

        var result = generator.GenerateTableOperations(
            table: CreateTableWithIdentity(),
            config: tableConfig
        );

        if (result is not StringOk ok)
        {
            Assert.Fail("Expected StringOk");
            return;
        }
        Assert.Contains("Microsoft.Data.SqlClient", ok.Value);
    }
}
