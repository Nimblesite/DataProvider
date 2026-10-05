using Nimblesite.Sql.Model;

namespace Nimblesite.DataProvider.Tests;

public sealed partial class CodeGenerationExtendedE2ETests
{
    // Implements [DP-CODEGEN-SOURCE-VALIDATION].
    [Theory]
    [InlineData("", "record Test();", "", "namespaceName cannot be null or empty")]
    [InlineData(" ", "", "class Foo { }", "namespaceName cannot be null or empty")]
    [InlineData(
        "Generated",
        "",
        "",
        "At least one of modelCode or dataAccessCode must be provided"
    )]
    [InlineData(
        "Generated",
        " ",
        " ",
        "At least one of modelCode or dataAccessCode must be provided"
    )]
    public void DefaultConfig_SourceFile_RejectsInvalidInput(
        string namespaceName,
        string modelCode,
        string dataAccessCode,
        string expectedMessage
    )
    {
        var config = new CodeGenerationConfig(
            getColumnMetadata: Nimblesite
                .DataProvider
                .SQLite
                .SqliteCodeGenerator
                .GetColumnMetadataFromSqlAsync
        );
        var error = Assert.IsType<StringError>(
            config.GenerateSourceFile(arg1: namespaceName, arg2: modelCode, arg3: dataAccessCode)
        );
        Assert.Equal(expectedMessage, error.Value.Message);
    }

    // Implements [DP-CODEGEN-POSTGRES-NULL-PARAMETER].
    [Fact]
    public void GenerateQueryMethod_Postgres_EmitsTypedNullParameter()
    {
        var result = DataAccessGenerator.GenerateQueryMethod(
            className: "GetHighValueOrdersExtensions",
            methodName: "GetHighValueOrders",
            returnTypeName: "OrderRecord",
            sql: "SELECT OrderId, CustomerName, Total FROM Orders WHERE Total > @Total",
            parameters: [new ParameterInfo(Name: "Total", SqlType: "REAL")],
            columns: OrderColumns,
            connectionType: "NpgsqlConnection"
        );
        var code = Assert.IsType<StringOk>(result).Value;
        Assert.Contains("NpgsqlTypes.NpgsqlDbType.Numeric", code);
        Assert.Contains("Value = DBNull.Value", code);
        Assert.Contains("new NpgsqlCommand(sql, connection)", code);
    }
}
