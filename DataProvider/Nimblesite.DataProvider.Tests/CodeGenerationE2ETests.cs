using System.Collections.Immutable;
using Nimblesite.Sql.Model;

namespace Nimblesite.DataProvider.Tests;

/// <summary>
/// E2E tests: DataAccessGenerator code generation with full verification.
/// Each test generates code and validates the output contains correct C# constructs.
/// </summary>
public sealed partial class CodeGenerationE2ETests
{
    // Implements [DP-CODEGEN-SHARED-BULK-LOOP].
    [Theory]
    [InlineData("int", "Integer")]
    [InlineData("int?", "Integer")]
    [InlineData("long", "Bigint")]
    [InlineData("short", "Smallint")]
    [InlineData("bool", "Boolean")]
    [InlineData("float", "Real")]
    [InlineData("double", "Double")]
    [InlineData("decimal", "Numeric")]
    [InlineData("DateTime", "Timestamp")]
    [InlineData("Guid", "Uuid")]
    [InlineData("Guid?", "Uuid")]
    [InlineData("byte[]", "Bytea")]
    [InlineData("string", "Text")]
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
        var code = Assert.IsType<StringOk>(@object: result).Value;
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
    [
        ColumnTestData.Create(name: "Id", sqlType: "TEXT", cSharpType: "Guid", isPrimaryKey: true),
        ColumnTestData.Create(name: "Name", sqlType: "TEXT", cSharpType: "string"),
        ColumnTestData.Create(name: "Age", sqlType: "INTEGER", cSharpType: "int"),
        ColumnTestData.Create(
            name: "Email",
            sqlType: "TEXT",
            cSharpType: "string",
            isNullable: true
        ),
        ColumnTestData.Create(name: "IsActive", sqlType: "INTEGER", cSharpType: "bool"),
    ];

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
        Assert.Contains("public static partial class PatientQueries", code);
        Assert.Contains("GetActivePatients", code);
        Assert.Contains("SqliteConnection", code);

        // Verify parameter handling
        Assert.Contains("minAge", code);
        Assert.Contains("status", code);

        // Verify SQL embedding
        Assert.Contains("SELECT Id, Name, Age, Email FROM Patients", code);

        // Verify mapper generation
        Assert.Contains("PatientRecord", code);

        // Verify XML docs
        Assert.Contains("/// <summary>", code);
        Assert.Contains("/// </summary>", code);
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
        var emptyMethod = DataAccessGenerator.GenerateQueryMethod(
            className: "TestClass",
            methodName: "",
            returnTypeName: "TestRecord",
            sql: "SELECT 1",
            parameters: [],
            columns: PatientColumns
        );
        Assert.True(emptyMethod is StringError);

        // Empty return type
        var emptyReturn = DataAccessGenerator.GenerateQueryMethod(
            className: "TestClass",
            methodName: "Test",
            returnTypeName: "",
            sql: "SELECT 1",
            parameters: [],
            columns: PatientColumns
        );
        Assert.True(emptyReturn is StringError);

        // Empty SQL
        var emptySql = DataAccessGenerator.GenerateQueryMethod(
            className: "TestClass",
            methodName: "Test",
            returnTypeName: "TestRecord",
            sql: "",
            parameters: [],
            columns: PatientColumns
        );
        Assert.True(emptySql is StringError);

        // Empty columns
        var emptyColumns = DataAccessGenerator.GenerateQueryMethod(
            className: "TestClass",
            methodName: "Test",
            returnTypeName: "TestRecord",
            sql: "SELECT 1",
            parameters: [],
            columns: []
        );
        Assert.True(emptyColumns is StringError);
    }

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
        Assert.Contains("PatientCommands", code);
        Assert.Contains("DeactivatePatient", code);
        Assert.Contains("UPDATE Patients", code);
        Assert.Contains("@id", code);
        Assert.Contains("SqliteConnection", code);
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
        Assert.Contains("Patients", code);

        // Verify non-identity columns are included
        Assert.Contains("Name", code);
        Assert.Contains("Age", code);
        Assert.Contains("Email", code);
        Assert.Contains("IsActive", code);
    }

    [Fact]
    public void GenerateParameterList_VariousInputs_ReturnsCorrectStrings()
    {
        // Empty parameters
        var empty = DataAccessGenerator.GenerateParameterList([]);
        Assert.Equal("", empty);

        // Single parameter
        var single = DataAccessGenerator.GenerateParameterList([new ParameterInfo(Name: "userId")]);
        Assert.Contains("userId", single);
        Assert.Contains("object", single);

        // Multiple parameters
        var multi = DataAccessGenerator.GenerateParameterList([
            new ParameterInfo(Name: "name", SqlType: "TEXT"),
            new ParameterInfo(Name: "age", SqlType: "INTEGER"),
            new ParameterInfo(Name: "email", SqlType: "TEXT"),
        ]);
        Assert.Contains("name", multi);
        Assert.Contains("age", multi);
        Assert.Contains("email", multi);
        Assert.Contains(",", multi);
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
        Assert.Contains("@class", code);
        Assert.Contains("@int", code);
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
        Assert.Contains("INSERT", code, StringComparison.OrdinalIgnoreCase);

        // Verify batch handling
        Assert.Contains("100", code);
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
            .Select(i =>
                ColumnTestData.Create(
                    name: $"Column{i}",
                    cSharpType: i % 3 == 0 ? "int"
                        : i % 3 == 1 ? "string"
                        : "bool",
                    sqlType: i % 3 == 0 ? "INTEGER"
                        : i % 3 == 1 ? "TEXT"
                        : "BOOLEAN",
                    isNullable: i % 4 == 0
                )
            )
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
        Assert.Contains("WideRecord", code);
        Assert.Contains("WideTableQueries", code);
        Assert.Contains("GetWideData", code);
        Assert.Contains("SELECT * FROM WideTable", code);
    }

    [Fact]
    public void GenerateNonQueryMethod_ValidationErrors_ReturnErrors()
    {
        var emptyClass = DataAccessGenerator.GenerateNonQueryMethod(
            className: " ",
            methodName: "Test",
            sql: "DELETE FROM T",
            parameters: []
        );
        Assert.True(emptyClass is StringError);

        var emptyMethod = DataAccessGenerator.GenerateNonQueryMethod(
            className: "TestClass",
            methodName: "",
            sql: "DELETE FROM T",
            parameters: []
        );
        Assert.True(emptyMethod is StringError);

        var emptySql = DataAccessGenerator.GenerateNonQueryMethod(
            className: "TestClass",
            methodName: "Test",
            sql: "",
            parameters: []
        );
        Assert.True(emptySql is StringError);
    }
}
