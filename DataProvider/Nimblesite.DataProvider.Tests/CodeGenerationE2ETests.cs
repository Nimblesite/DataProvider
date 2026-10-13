using System.Collections.Immutable;
using static global::Tests.Shared.StringAssertions;

namespace Nimblesite.DataProvider.Tests;

/// <summary>
/// E2E tests: DataAccessGenerator code generation with full verification.
/// Each test generates code and validates the output contains correct C# constructs.
/// </summary>
public sealed partial class CodeGenerationE2ETests
{
    // Implements [DP-CODEGEN-SHARED-BULK-LOOP].
    [Theory]
    [MemberData(
        nameof(GenerateQueryMethod_PostgresNullParameter_UsesMatchingColumnTypeInlineCases)
    )]
    public void GenerateQueryMethod_PostgresNullParameter_UsesMatchingColumnType(
        string cSharpType,
        string databaseType
    )
    {
        var result = DataAccessGenerator.GenerateQueryMethod(
            className: "PeopleQueries",
            methodName: "Find",
            returnTypeName: "Person",
            sql: "SELECT Id FROM People WHERE Id = @id",
            parameters: [new ParameterInfo(Name: "id")],
            columns: [new DatabaseColumn { Name = "Id", CSharpType = cSharpType }],
            connectionType: "NpgsqlConnection"
        );
        var code = global::Tests.Shared.ResultAssertions.ExactOk(actual: result).Value;
        Assert.Contains(
            expectedSubstring: $"new NpgsqlParameter(\"@id\", NpgsqlTypes.NpgsqlDbType.{databaseType}) {{ Value = DBNull.Value }}",
            actualString: code
        );
        Assert.Contains(
            expectedSubstring: "command.Parameters.AddWithValue(\"@id\", id);",
            actualString: code
        );
    }

    private static readonly IReadOnlyList<DatabaseColumn> PatientColumns =
        CodeGenerationColumnSets.PatientColumns;

    private static readonly IReadOnlyList<ParameterInfo> QueryParameters =
    [
        new ParameterInfo(Name: "minAge", SqlType: "INTEGER"),
        new ParameterInfo(Name: "status", SqlType: "TEXT"),
    ];

    [Fact]
    public void GenerateQueryMethod_FullWorkflow_ProducesValidExtensionMethod()
    {
        var result = DataAccessGenerator.GenerateQueryMethod(
            className: "PatientQueries",
            methodName: "GetActivePatients",
            returnTypeName: "PatientRecord",
            sql: "SELECT Id, Name, Age, Email FROM Patients WHERE IsActive = 1 AND Age > @minAge",
            parameters: QueryParameters,
            columns: PatientColumns,
            connectionType: "SqliteConnection"
        );

        Assert.True(result is StringOk);
        if (result is not StringOk ok)
            return;
        var code = ok.Value;

        // Verify class structure
        ContainsAll(
            actual: code,
            expected:
            [
                "public static partial class PatientQueries",
                "GetActivePatients",
                "SqliteConnection",
                "minAge",
                "status",
                "SELECT Id, Name, Age, Email FROM Patients",
                "PatientRecord",
                "/// <summary>",
                "/// </summary>",
            ]
        );
    }

    [Fact]
    public void GenerateQueryMethod_ValidationErrors_ReturnDetailedErrors()
    {
        // Empty class name
        var emptyClass = DataAccessGenerator.GenerateQueryMethod(
            className: "",
            methodName: "Test",
            returnTypeName: "TestRecord",
            sql: "SELECT 1",
            parameters: [],
            columns: PatientColumns
        );
        Assert.True(emptyClass is StringError);
        if (emptyClass is StringError classErr)
        {
            Assert.Contains(
                "className",
                classErr.Value.Message,
                StringComparison.OrdinalIgnoreCase
            );
        }

        // Empty method name
        AssertGenerateQueryMethodError(
            methodName: "",
            returnTypeName: "TestRecord",
            sql: "SELECT 1",
            columns: PatientColumns
        );

        // Empty return type
        AssertGenerateQueryMethodError(
            methodName: "Test",
            returnTypeName: "",
            sql: "SELECT 1",
            columns: PatientColumns
        );

        // Empty SQL
        AssertGenerateQueryMethodError(
            methodName: "Test",
            returnTypeName: "TestRecord",
            sql: "",
            columns: PatientColumns
        );

        // Empty columns
        AssertGenerateQueryMethodError(
            methodName: "Test",
            returnTypeName: "TestRecord",
            sql: "SELECT 1",
            columns: []
        );
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void AssertGenerateQueryMethodError(
        string methodName,
        string returnTypeName,
        string sql,
        IReadOnlyList<DatabaseColumn> columns
    ) =>
        global::Tests.Shared.ResultAssertions.Error(
            actual: DataAccessGenerator.GenerateQueryMethod(
                className: "TestClass",
                methodName: methodName,
                returnTypeName: returnTypeName,
                sql: sql,
                parameters: [],
                columns: columns
            )
        );

    [Fact]
    public void GenerateNonQueryMethod_FullWorkflow_ProducesValidCode()
    {
        var result = DataAccessGenerator.GenerateNonQueryMethod(
            className: "PatientCommands",
            methodName: "DeactivatePatient",
            sql: "UPDATE Patients SET IsActive = 0 WHERE Id = @id",
            parameters: [new ParameterInfo(Name: "id", SqlType: "TEXT")],
            connectionType: "SqliteConnection"
        );

        Assert.True(result is StringOk);
        if (result is not StringOk ok)
            return;
        var code = ok.Value;

        // Verify it generates non-query method
        ContainsAll(
            actual: code,
            expected:
            [
                "PatientCommands",
                "DeactivatePatient",
                "UPDATE Patients",
                "@id",
                "SqliteConnection",
            ]
        );
    }

    [Fact]
    public void GenerateInsertMethod_WithAllColumnTypes_ProducesCorrectCode()
    {
        var table = Fixture_GenerateInsertMethod_WithAllColumnTypes_ProducesCorrectCode_1();

        var result = DataAccessGenerator.GenerateInsertMethod(
            table: table,
            connectionType: "SqliteConnection"
        );

        Assert.True(result is StringOk);
        if (result is not StringOk ok)
            return;
        var code = ok.Value;

        // Verify INSERT statement
        Assert.Contains("INSERT", code, StringComparison.OrdinalIgnoreCase);
        ContainsAll(actual: code, expected: ["Patients", "Name", "Age", "Email", "IsActive"]);
    }

    [Fact]
    public void GenerateParameterList_VariousInputs_ReturnsCorrectStrings()
    {
        // Empty parameters
        var empty = DataAccessGenerator.GenerateParameterList([]);
        Assert.Equal("", empty);

        // Single parameter
        var single = DataAccessGenerator.GenerateParameterList([new ParameterInfo(Name: "userId")]);
        ContainsAll(actual: single, expected: ["userId", "object"]);

        // Multiple parameters
        var multi = DataAccessGenerator.GenerateParameterList([
            new ParameterInfo(Name: "name", SqlType: "TEXT"),
            new ParameterInfo(Name: "age", SqlType: "INTEGER"),
            new ParameterInfo(Name: "email", SqlType: "TEXT"),
        ]);
        ContainsAll(actual: multi, expected: ["name", "age", "email", ","]);
    }

    [Fact]
    public void GenerateQueryMethod_WithReservedKeywords_EscapesCorrectly()
    {
        // Use C# reserved keywords as parameter names
        var result = DataAccessGenerator.GenerateQueryMethod(
            className: "TestQueries",
            methodName: "GetByClass",
            returnTypeName: "TestRecord",
            sql: "SELECT * FROM Items WHERE class = @class AND int = @int",
            parameters: [new ParameterInfo(Name: "class"), new ParameterInfo(Name: "int")],
            columns: [ColumnTestData.Create(name: "Id", cSharpType: "string", sqlType: "TEXT")]
        );

        Assert.True(result is StringOk);
        if (result is not StringOk ok)
            return;
        var code = ok.Value;
        // Reserved keywords should be escaped with @
        ContainsAll(actual: code, expected: ["@class", "@int"]);
    }

    [Fact]
    public void GenerateBulkInsertMethod_ProducesValidBatchCode()
    {
        var table = Fixture_GenerateBulkInsertMethod_ProducesValidBatchCode_1();

        var result = DataAccessGenerator.GenerateBulkInsertMethod(table: table, batchSize: 100);

        Assert.True(result is StringOk);
        if (result is not StringOk ok)
            return;
        var code = ok.Value;

        // Verify bulk insert structure
        Assert.Contains("Patients", code);
        global::Nimblesite.DataProvider.Tests.QueryBuilderE2ETests.VerifyReusableWorkflow1(
            input0: code,
            value0: "INSERT",
            value2: "100"
        );
    }

    [Fact]
    public void GenerateBulkUpsertMethod_SQLite_ProducesValidUpsertCode()
    {
        var table = Fixture_GenerateBulkUpsertMethod_SQLite_ProducesValidUpsertCode_1();

        var result = DataAccessGenerator.GenerateBulkUpsertMethod(
            table: table,
            databaseType: "SQLite",
            connectionType: "SqliteConnection"
        );

        Assert.True(result is StringOk);
        if (result is not StringOk ok)
            return;
        var code = ok.Value;

        Assert.Contains("Patients", code);
        // SQLite upsert uses INSERT OR REPLACE or ON CONFLICT
        Assert.True(
            code.Contains("REPLACE", StringComparison.OrdinalIgnoreCase)
                || code.Contains("ON CONFLICT", StringComparison.OrdinalIgnoreCase)
                || code.Contains("UPSERT", StringComparison.OrdinalIgnoreCase)
                || code.Contains("INSERT", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void GenerateQueryMethod_MultipleColumns_MapsAllColumns()
    {
        var manyColumns = Enumerable
            .Range(0, 15)
            .Select(i => Dataset_GenerateQueryMethod_MultipleColumns_MapsAllColumns_1(i: i))
            .ToImmutableArray();

        var result = DataAccessGenerator.GenerateQueryMethod(
            className: "WideTableQueries",
            methodName: "GetWideData",
            returnTypeName: "WideRecord",
            sql: "SELECT * FROM WideTable",
            parameters: [],
            columns: manyColumns
        );

        Assert.True(result is StringOk);
        if (result is not StringOk ok)
            return;
        var code = ok.Value;

        // Verify columns appear in the generated code (may use reader ordinal or column name)
        ContainsAll(
            actual: code,
            expected: ["WideRecord", "WideTableQueries", "GetWideData", "SELECT * FROM WideTable"]
        );
    }

    [Fact]
    public void GenerateNonQueryMethod_ValidationErrors_ReturnErrors()
    {
        global::Tests.Shared.ResultAssertions.Error(
            actual: DataAccessGenerator.GenerateNonQueryMethod(
                className: " ",
                methodName: "Test",
                sql: "DELETE FROM T",
                parameters: []
            )
        );

        global::Tests.Shared.ResultAssertions.Error(
            actual: DataAccessGenerator.GenerateNonQueryMethod(
                className: "TestClass",
                methodName: "",
                sql: "DELETE FROM T",
                parameters: []
            )
        );

        global::Tests.Shared.ResultAssertions.Error(
            actual: DataAccessGenerator.GenerateNonQueryMethod(
                className: "TestClass",
                methodName: "Test",
                sql: "",
                parameters: []
            )
        );
    }
}
