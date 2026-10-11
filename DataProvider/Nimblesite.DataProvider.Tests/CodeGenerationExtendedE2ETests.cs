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

    private static Task<Result<IReadOnlyList<DatabaseColumn>, SqlError>> MockGetColumnMetadata(
        string connectionString,
        string sql,
        IEnumerable<ParameterInfo> parameters
    ) => CodeGenerationTestDoubles.ColumnMetadata(columns: AllJoinedColumns);

    private static CodeGenerationConfig CreateConfig() =>
        new(getColumnMetadata: MockGetColumnMetadata);

    [Fact]
    public void GenerateGroupedQueryMethod_ViaConfig_ProducesParentChildCode()
    {
        var ok = Ok(
            read: () =>
                CreateConfig()
                    .GenerateGroupedQueryMethod(
                        "OrderQueries",
                        "GetOrdersWithItems",
                        "SELECT o.OrderId, o.CustomerName, o.Total, li.LineItemId, li.ProductName, li.Quantity FROM Orders o JOIN LineItems li ON o.OrderId = li.OrderId",
                        Array.Empty<ParameterInfo>(),
                        AllJoinedColumns,
                        CreateOrderGroupingConfig(),
                        "SqliteConnection"
                    ),
            message: "Expected StringOk"
        );
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
        var ok = Ok(
            read: () =>
                CreateConfig()
                    .GenerateGroupedQueryMethod(
                        "OrderQueries",
                        "GetOrdersByCustomer",
                        "SELECT o.OrderId FROM Orders o JOIN LineItems li ON o.OrderId = li.OrderId WHERE o.CustomerId = @customerId",
                        new List<ParameterInfo> { new(Name: "customerId", SqlType: "TEXT") },
                        AllJoinedColumns,
                        CreateOrderGroupingConfig(),
                        "SqliteConnection"
                    ),
            message: "Expected StringOk"
        );
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

        VerifyReusableWorkflow15(input0: result, value1: "className");
    }

    [Fact]
    public void GenerateGroupedQueryMethod_EmptyColumns_ReturnsError()
    {
        global::Tests.Shared.ResultAssertions.Error(
            actual: CreateConfig()
                .GenerateGroupedQueryMethod(
                    "TestClass",
                    "TestMethod",
                    "SELECT 1",
                    Array.Empty<ParameterInfo>(),
                    Array.Empty<DatabaseColumn>(),
                    CreateOrderGroupingConfig(),
                    "SqliteConnection"
                )
        );
    }

    [Fact]
    public void GenerateTableOperations_WithInsertAndUpdate_ProducesAllMethods()
    {
        var code = GenerateConfiguredTableOperationOk(
            connectionType: "SqliteConnection",
            schema: "main",
            generateUpdate: true
        );

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

        VerifyReusableWorkflow15(input0: result, value1: "table");
    }

    [Fact]
    public void GenerateTableOperations_NullConfig_ReturnsError()
    {
        var result = new DefaultTableOperationGenerator().GenerateTableOperations(
            table: CreateTableWithIdentity(),
            config: null!
        );

        VerifyReusableWorkflow15(input0: result, value1: "config");
    }

    [Fact]
    public void GenerateInsertMethod_ExcludesIdentityColumns()
    {
        var ok = Ok(
            read: () =>
                new DefaultTableOperationGenerator(
                    connectionType: "SqliteConnection"
                ).GenerateInsertMethod(table: CreateTableWithIdentity()),
            message: "Expected StringOk"
        );
        var code = ok.Value;

        Assert.Contains("INSERT", code, StringComparison.OrdinalIgnoreCase);
        ContainsAll(actual: code, expected: ["Products", "Name", "Price"]);
        Assert.DoesNotContain("RowNum", code);
    }

    [Fact]
    public void GenerateUpdateMethod_UsesPrimaryKeyInWhere()
    {
        var ok = Ok(
            read: () =>
                new DefaultTableOperationGenerator(
                    connectionType: "SqliteConnection"
                ).GenerateUpdateMethod(table: CreateTableWithIdentity()),
            message: "Expected StringOk"
        );
        var code = ok.Value;

        global::Nimblesite.DataProvider.Tests.QueryBuilderE2ETests.VerifyReusableWorkflow1(
            input0: code,
            value0: "UPDATE",
            value2: "Products"
        );
        global::Nimblesite.DataProvider.Tests.QueryBuilderE2ETests.VerifyReusableWorkflow1(
            input0: code,
            value0: "WHERE",
            value2: "Id"
        );
    }

    [Fact]
    public void GenerateModelType_ProducesRecordWithProperties()
    {
        var ok = Ok(
            read: () =>
                new DefaultCodeTemplate().GenerateModelType(
                    typeName: "PatientRecord",
                    columns: OrderColumns
                ),
            message: "Expected StringOk"
        );
        var code = ok.Value;

        ContainsAll(actual: code, expected: ["PatientRecord", "OrderId", "CustomerName", "Total"]);
    }

    [Fact]
    public void GenerateModelType_EmptyTypeName_ReturnsError()
    {
        global::Tests.Shared.ResultAssertions.Error(
            actual: new DefaultCodeTemplate().GenerateModelType(typeName: "", columns: OrderColumns)
        );
    }

    [Fact]
    public void GenerateDataAccessMethod_ProducesExtensionMethod()
    {
        var ok = Ok(
            read: () =>
                new DefaultCodeTemplate().GenerateDataAccessMethod(
                    methodName: "GetHighValueOrders",
                    returnTypeName: "OrderRecord",
                    sql: "SELECT OrderId, CustomerName, Total FROM Orders WHERE Total > @minTotal",
                    parameters: new List<ParameterInfo> { new(Name: "minTotal", SqlType: "REAL") },
                    columns: OrderColumns
                ),
            message: "Expected StringOk"
        );
        VerifyReusableWorkflow6(
            input0: ok,
            input1: ContainsAll,
            value0: "GetHighValueOrdersExtensions",
            value1: "GetHighValueOrders",
            value2: "OrderRecord",
            value3: "minTotal",
            value4: "SqliteConnection"
        );
    }

    [Fact]
    public void GenerateSourceFile_CombinesModelAndDataAccess()
    {
        var ok = Ok(
            read: () =>
                new DefaultCodeTemplate().GenerateSourceFile(
                    namespaceName: "MyApp.Generated",
                    modelCode: "public sealed record OrderRecord(Guid OrderId, string? CustomerName);",
                    dataAccessCode: "public static partial class OrderQueries { }"
                ),
            message: "Expected StringOk"
        );
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
        var ok = Ok(
            read: () =>
                new DefaultCodeTemplate().GenerateSourceFile(
                    namespaceName: "MyApp",
                    modelCode: "public sealed record Widget(string Name);",
                    dataAccessCode: ""
                ),
            message: "Expected StringOk"
        );
        Assert.Contains("Widget", ok.Value);
        Assert.Contains("namespace MyApp;", ok.Value);
    }

    [Fact]
    public void GenerateGroupedModels_ProducesParentAndChildTypes()
    {
        var ok = Ok(
            read: () =>
                new DefaultCodeTemplate().GenerateGroupedModels(
                    groupingConfig: CreateOrderGroupingConfig(),
                    columns: AllJoinedColumns
                ),
            message: "Expected StringOk"
        );
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
        var code = GenerateConfiguredTableOperationOk(
            connectionType: "SqliteConnection",
            schema: "main",
            generateUpdate: false
        );
        Assert.Contains("INSERT", code, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ProductsExtensions", code);
    }

    [Fact]
    public void DefaultTableOperationGenerator_SqlServerConnectionType_UsesCorrectNamespace()
    {
        var code = GenerateConfiguredTableOperationOk(
            connectionType: "SqlConnection",
            schema: "dbo",
            generateUpdate: true
        );
        Assert.Contains("Microsoft.Data.SqlClient", code);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static string GenerateConfiguredTableOperationOk(
        string connectionType,
        string schema,
        bool generateUpdate
    ) =>
        Ok(
            read: () =>
                GenerateConfiguredTableOperation(
                    value0: connectionType,
                    value2: schema,
                    value4: generateUpdate
                ),
            message: "Expected StringOk"
        ).Value;

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

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyReusableWorkflow6(
        Result<string, Sql.Model.SqlError>.Ok<string, Sql.Model.SqlError> input0,
        Action<string?, System.Collections.Immutable.ImmutableArray<string>> input1,
        string value0,
        string value1,
        string value2,
        string value3,
        string value4
    )
    {
        var code = input0.Value;

        input1(arg1: code, arg2: [value0, value1, value2, value3, value4]);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyReusableWorkflow15(
        Result<string, Sql.Model.SqlError> input0,
        string value1
    )
    {
        var err = global::Tests.Shared.ResultAssertions.Expected<StringError>(
            actual: input0,
            message: "Expected StringError"
        );
        Assert.Contains(value1, err.Value.Message, StringComparison.OrdinalIgnoreCase);
    }
}
