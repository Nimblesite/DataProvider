using Nimblesite.Sql.Model;
using Outcome;
using static global::Tests.Shared.ResultAssertions;
using static global::Tests.Shared.StringAssertions;

namespace Nimblesite.DataProvider.Tests;

public sealed partial class CodeGenerationExtendedE2ETests
{
    private static readonly IReadOnlyList<DatabaseColumn> OrderColumns =
    [
        ColumnTestData.Create(
            name: "OrderId",
            sqlType: "TEXT",
            cSharpType: "Guid",
            isPrimaryKey: true
        ),
        ColumnTestData.Create(name: "CustomerName", sqlType: "TEXT", cSharpType: "string"),
        ColumnTestData.Create(name: "Total", sqlType: "REAL", cSharpType: "decimal"),
    ];

    private static readonly IReadOnlyList<DatabaseColumn> AllJoinedColumns =
    [
        ColumnTestData.Create(name: "OrderId", sqlType: "TEXT", cSharpType: "Guid"),
        ColumnTestData.Create(name: "CustomerName", sqlType: "TEXT", cSharpType: "string"),
        ColumnTestData.Create(name: "Total", sqlType: "REAL", cSharpType: "decimal"),
        ColumnTestData.Create(name: "LineItemId", sqlType: "TEXT", cSharpType: "Guid"),
        ColumnTestData.Create(name: "ProductName", sqlType: "TEXT", cSharpType: "string"),
        ColumnTestData.Create(name: "Quantity", sqlType: "INTEGER", cSharpType: "int"),
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
                ColumnTestData.Create(
                    name: "Id",
                    sqlType: "TEXT",
                    cSharpType: "Guid",
                    isPrimaryKey: true
                ),
                ColumnTestData.Create(
                    name: "RowNum",
                    sqlType: "INTEGER",
                    cSharpType: "int",
                    isIdentity: true
                ),
                ColumnTestData.Create(name: "Name", sqlType: "TEXT", cSharpType: "string"),
                ColumnTestData.Create(name: "Price", sqlType: "REAL", cSharpType: "decimal"),
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

        var ok = Ok(actual: result, message: "Expected StringOk");
        var code = ok.Value;

        ContainsAll(
            actual: code,
            expected:
            [
                "public static partial class OrderQueries",
                "GetOrdersWithItemsAsync",
                "SqliteConnection",
                "SELECT o.OrderId",
                "GroupResults",
                "Order",
                "LineItem",
            ]
        );
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

        var ok = Ok(actual: result, message: "Expected StringOk");
        var code = ok.Value;

        ContainsAll(
            actual: code,
            expected: ["customerId", "@customerId", "AddWithValue", "GetOrdersByCustomerAsync"]
        );
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

        var err = global::Tests.Shared.ResultAssertions.Expected<StringError>(
            actual: result,
            message: "Expected StringError"
        );
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
        var result = GenerateConfiguredTableOperation(
            value0: "SqliteConnection",
            value2: "main",
            value4: true
        );

        var ok = Ok(actual: result, message: "Expected StringOk");
        var code = ok.Value;

        Assert.Contains("ProductsExtensions", code);
        ContainsAll(
            actual: code,
            expected: ["INSERT", "UPDATE"],
            comparison: StringComparison.OrdinalIgnoreCase
        );
        ContainsAll(actual: code, expected: ["namespace Generated", "Microsoft.Data.Sqlite"]);
    }

    [Fact]
    public void GenerateTableOperations_NullTable_ReturnsError()
    {
        var generator = new DefaultTableOperationGenerator();
        var tableConfig = new TableConfig { Name = "Test", GenerateInsert = true };

        var result = generator.GenerateTableOperations(table: null!, config: tableConfig);

        var err = global::Tests.Shared.ResultAssertions.Expected<StringError>(
            actual: result,
            message: "Expected StringError"
        );
        Assert.Contains("table", err.Value.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenerateTableOperations_NullConfig_ReturnsError()
    {
        var result = new DefaultTableOperationGenerator().GenerateTableOperations(
            table: CreateTableWithIdentity(),
            config: null!
        );

        var err = global::Tests.Shared.ResultAssertions.Expected<StringError>(
            actual: result,
            message: "Expected StringError"
        );
        Assert.Contains("config", err.Value.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenerateInsertMethod_ExcludesIdentityColumns()
    {
        var result = new DefaultTableOperationGenerator(
            connectionType: "SqliteConnection"
        ).GenerateInsertMethod(table: CreateTableWithIdentity());

        var ok = Ok(actual: result, message: "Expected StringOk");
        var code = ok.Value;

        Assert.Contains("INSERT", code, StringComparison.OrdinalIgnoreCase);
        ContainsAll(actual: code, expected: ["Products", "Name", "Price"]);
        Assert.DoesNotContain("RowNum", code);
    }

    [Fact]
    public void GenerateUpdateMethod_UsesPrimaryKeyInWhere()
    {
        var result = new DefaultTableOperationGenerator(
            connectionType: "SqliteConnection"
        ).GenerateUpdateMethod(table: CreateTableWithIdentity());

        var ok = Ok(actual: result, message: "Expected StringOk");
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

        var ok = Ok(actual: result, message: "Expected StringOk");
        var code = ok.Value;

        ContainsAll(actual: code, expected: ["PatientRecord", "OrderId", "CustomerName", "Total"]);
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

        var ok = Ok(actual: result, message: "Expected StringOk");
        var code = ok.Value;

        ContainsAll(
            actual: code,
            expected:
            [
                "GetHighValueOrdersExtensions",
                "GetHighValueOrders",
                "OrderRecord",
                "minTotal",
                "SqliteConnection",
            ]
        );
    }

    [Fact]
    public void GenerateSourceFile_CombinesModelAndDataAccess()
    {
        var result = new DefaultCodeTemplate().GenerateSourceFile(
            namespaceName: "MyApp.Generated",
            modelCode: "public sealed record OrderRecord(Guid OrderId, string? CustomerName);",
            dataAccessCode: "public static partial class OrderQueries { }"
        );

        var ok = Ok(actual: result, message: "Expected StringOk");
        var code = ok.Value;

        ContainsAll(
            actual: code,
            expected:
            [
                "namespace MyApp.Generated;",
                "using System;",
                "using Microsoft.Data.Sqlite;",
                "using Outcome;",
                "OrderRecord",
                "OrderQueries",
            ]
        );
        _ = global::Tests.Shared.GeneratedCodeCompiler.Compile(source: code);
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

        var ok = Ok(actual: result, message: "Expected StringOk");
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

        var ok = Ok(actual: result, message: "Expected StringOk");
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
        var result = GenerateConfiguredTableOperation(
            value0: "SqliteConnection",
            value2: "main",
            value4: false
        );

        var ok = Ok(actual: result, message: "Expected StringOk");
        Assert.Contains("INSERT", ok.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ProductsExtensions", ok.Value);
    }

    [Fact]
    public void DefaultTableOperationGenerator_SqlServerConnectionType_UsesCorrectNamespace()
    {
        var result = GenerateConfiguredTableOperation(
            value0: "SqlConnection",
            value2: "dbo",
            value4: true
        );

        var ok = Ok(actual: result, message: "Expected StringOk");
        Assert.Contains("Microsoft.Data.SqlClient", ok.Value);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static global::Outcome.Result<
        global::System.String,
        global::Nimblesite.Sql.Model.SqlError
    > GenerateConfiguredTableOperation(string value0, string value2, bool value4)
    {
        var generator = new DefaultTableOperationGenerator(connectionType: value0);
        var tableConfig = Dataset_GenerateConfiguredTableOperation_1(
            value2: value2,
            value4: value4
        );

        var result = generator.GenerateTableOperations(
            table: CreateTableWithIdentity(),
            config: tableConfig
        );

        return result;
    }
}
