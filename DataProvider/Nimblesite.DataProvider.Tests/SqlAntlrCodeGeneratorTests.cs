using System.Collections.Frozen;
using static global::Tests.Shared.ResultAssertions;

namespace Nimblesite.DataProvider.Tests;

// Implements [CON-SHARED-CORE]. Targets the dialect-agnostic
// SqlAntlrCodeGenerator: validation branches, the standard-version
// failure-propagation paths, and the entire grouped-version pipeline
// (raw record / grouped method / grouped models, plus their failure arms).
public sealed partial class SqlAntlrCodeGeneratorTests
{
    private static readonly IReadOnlyList<DatabaseColumn> GroupColumns =
    [
        ColumnTestData.Create(
            name: "OrderId",
            sqlType: "INTEGER",
            cSharpType: "int",
            isPrimaryKey: true
        ),
        ColumnTestData.Create(name: "OrderDate", sqlType: "TEXT", cSharpType: "string"),
        ColumnTestData.Create(name: "LineId", sqlType: "INTEGER", cSharpType: "int"),
        ColumnTestData.Create(name: "LineQty", sqlType: "INTEGER", cSharpType: "int"),
    ];

    private static SelectStatement MakeStatement(params string[] paramNames) =>
        new() { Parameters = paramNames.Select(n => new ParameterInfo(n)).ToFrozenSet() };

    private static CodeGenerationConfig MakeConfig() =>
        new(
            (cs, sql, p) =>
                Task.FromResult<Result<IReadOnlyList<DatabaseColumn>, SqlError>>(
                    new Result<IReadOnlyList<DatabaseColumn>, SqlError>.Ok<
                        IReadOnlyList<DatabaseColumn>,
                        SqlError
                    >(GroupColumns)
                )
        )
        {
            ConnectionType = "SqliteConnection",
            TargetNamespace = "Generated",
        };

    private static GroupingConfig MakeGrouping() =>
        new(
            QueryName: "OrderWithLines",
            GroupingStrategy: "parent-child",
            ParentEntity: new EntityConfig(
                Name: "Order",
                KeyColumns: ["OrderId"],
                Columns: ["OrderId", "OrderDate"]
            ),
            ChildEntity: new EntityConfig(
                Name: "OrderLine",
                KeyColumns: ["LineId"],
                Columns: ["LineId", "LineQty"],
                ParentKeyColumns: ["OrderId"]
            )
        );

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(IndependentScenarioData4))]
    public void Rejects_empty_file_name_ScenarioCases(
        string scenarioName,
        System.String scenario0,
        System.String scenario1
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: scenarioName));

        global::Tests.Shared.ResultAssertions.Error(
            actual: SqlAntlrCodeGenerator.GenerateCodeWithMetadata(
                scenario0,
                scenario1,
                MakeStatement(),
                GroupColumns,
                MakeConfig()
            )
        );
    }

    [Fact]
    public void Rejects_null_statement()
    {
        global::Tests.Shared.ResultAssertions.Error(
            actual: SqlAntlrCodeGenerator.GenerateCodeWithMetadata(
                "Order",
                "SELECT 1",
                null!,
                GroupColumns,
                MakeConfig()
            )
        );
    }

    [Fact]
    public void Rejects_null_column_metadata()
    {
        global::Tests.Shared.ResultAssertions.Error(
            actual: SqlAntlrCodeGenerator.GenerateCodeWithMetadata(
                "Order",
                "SELECT 1",
                MakeStatement(),
                null!,
                MakeConfig()
            )
        );
    }

    [Fact]
    public void Rejects_null_config()
    {
        global::Tests.Shared.ResultAssertions.Error(
            actual: SqlAntlrCodeGenerator.GenerateCodeWithMetadata(
                "Order",
                "SELECT 1",
                MakeStatement(),
                GroupColumns,
                null!
            )
        );
    }

    [Fact]
    public void Propagates_model_failure() =>
        AssertGenerateCodeFailure(
            configure: config =>
                config with
                {
                    GenerateModelType = (_, _) => new StringError(new SqlError("model boom")),
                },
            expectedMessage: "model boom"
        );

    [Fact]
    public void Propagates_data_access_failure() =>
        AssertGenerateCodeFailure(
            configure: config =>
                config with
                {
                    GenerateDataAccessMethod = (_, _, _, _, _, _) =>
                        new StringError(new SqlError("access boom")),
                },
            expectedMessage: "access boom"
        );

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void AssertGenerateCodeFailure(
        Func<CodeGenerationConfig, CodeGenerationConfig> configure,
        string expectedMessage
    )
    {
        var config = configure(MakeConfig());
        var result = SqlAntlrCodeGenerator.GenerateCodeWithMetadata(
            "Order",
            "SELECT 1",
            MakeStatement(),
            GroupColumns,
            config
        );

        var err = Expected<StringError>(actual: result);
        Assert.Contains(expectedMessage, err.Value.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Grouped_version_produces_source()
    {
        var ok = Ok(read: () =>
            SqlAntlrCodeGenerator.GenerateCodeWithMetadata(
                "OrderWithLines",
                "SELECT OrderId, OrderDate, LineId, LineQty FROM Orders",
                MakeStatement(),
                GroupColumns,
                MakeConfig(),
                MakeGrouping()
            )
        );
        Assert.Contains("Generated", ok.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Grouped_version_propagates_raw_failure()
    {
        var config = MakeConfig() with
        {
            GenerateRawRecordType = (_, _) => new StringError(new SqlError("raw boom")),
        };

        GenerateAndAssertMetadataCode(input0: config, value2: "raw boom");
    }

    [Fact]
    public void Grouped_version_propagates_method_failure()
    {
        var config = MakeConfig() with
        {
            GenerateGroupedQueryMethod = (_, _, _, _, _, _, _) =>
                new StringError(new SqlError("method boom")),
        };

        GenerateAndAssertMetadataCode(input0: config, value2: "method boom");
    }

    [Fact]
    public void Grouped_version_propagates_models_failure()
    {
        var config = MakeConfig() with
        {
            GenerateGroupedModels = (_, _, _, _, _) => new StringError(new SqlError("models boom")),
        };

        GenerateAndAssertMetadataCode(input0: config, value2: "models boom");
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void GenerateAndAssertMetadataCode(
        global::Nimblesite.DataProvider.Core.CodeGeneration.CodeGenerationConfig input0,
        string value2
    )
    {
        var result = SqlAntlrCodeGenerator.GenerateCodeWithMetadata(
            "OrderWithLines",
            "SELECT 1",
            MakeStatement(),
            GroupColumns,
            input0,
            MakeGrouping()
        );
        Assert.True(result is StringError);
        Assert.Contains(value2, (Error(actual: result)).Value.Message, StringComparison.Ordinal);
    }
}
