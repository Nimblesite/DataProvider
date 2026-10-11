using Nimblesite.Sql.Model;
using static global::Tests.Shared.StringAssertions;

namespace Nimblesite.DataProvider.Tests;

public sealed partial class CodeGenerationExtendedE2ETests
{
    // Implements [DP-CODEGEN-SOURCE-VALIDATION].
    [Theory]
    [MemberData(nameof(DefaultConfig_SourceFile_RejectsInvalidInputInlineCases))]
    public void DefaultConfig_SourceFile_RejectsInvalidInput(
        string namespaceName,
        string modelCode,
        string dataAccessCode,
        string expectedMessage
    )
    {
        var config = Dataset_DefaultConfig_SourceFile_RejectsInvalidInput_1();
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
        ContainsAll(
            actual: code,
            expected:
            [
                "NpgsqlTypes.NpgsqlDbType.Numeric",
                "Value = DBNull.Value",
                "new NpgsqlCommand(sql, connection)",
            ]
        );
    }
}
