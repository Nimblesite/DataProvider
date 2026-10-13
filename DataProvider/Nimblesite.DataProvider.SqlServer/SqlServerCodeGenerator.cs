using System.Collections.Immutable;
using Microsoft.Data.SqlClient;
using Nimblesite.DataProvider.Core.CodeGeneration;
using CodeResult = Outcome.Result<string, Nimblesite.Sql.Model.SqlError>;
using ColumnsOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.DataProvider.Core.DatabaseColumn>,
    Nimblesite.Sql.Model.SqlError
>.Ok<
    System.Collections.Generic.IReadOnlyList<Nimblesite.DataProvider.Core.DatabaseColumn>,
    Nimblesite.Sql.Model.SqlError
>;
using ParseOk = Outcome.Result<Nimblesite.Sql.Model.SelectStatement, string>.Ok<
    Nimblesite.Sql.Model.SelectStatement,
    string
>;
using SqlError = Nimblesite.Sql.Model.SqlError;

namespace Nimblesite.DataProvider.SqlServer;

/// <summary>SQL Server adapter for shared code generation. Implements [CON-SHARED-CORE].</summary>
public static class SqlServerCodeGenerator
{
    /// <summary>Creates SQL Server metadata inspection and shared source generation functions.</summary>
    public static CodeGenerationConfig CreateConfig(string targetNamespace = "Generated") =>
        new(
            getColumnMetadata: new AdoNetDatabaseEffects(
                connectionFactory: value => new SqlConnection(value),
                typeMapper: MapType,
                behavior: System.Data.CommandBehavior.SchemaOnly
                    | System.Data.CommandBehavior.KeyInfo,
                parameterValue: parameter =>
                    parameter.Name.Contains("id", StringComparison.OrdinalIgnoreCase)
                        ? Guid.Empty
                        : DummyParameterValues.GetDummyValueForParameter(parameter: parameter)
            ).GetColumnMetadataFromSqlAsync,
            tableOperationGenerator: new DefaultTableOperationGenerator(
                connectionType: "SqlConnection"
            )
        )
        {
            ConnectionType = "Microsoft.Data.SqlClient.SqlConnection",
            TargetNamespace = targetNamespace,
            GenerateSourceFile = (namespaceName, model, methods) =>
                CodeGenerationConfig.GenerateSourceForConnection(
                    namespaceName: namespaceName,
                    modelCode: model,
                    dataAccessCode: methods,
                    connectionNamespace: "Microsoft.Data.SqlClient"
                ),
        };

    /// <summary>Generates a query extension using validated T-SQL and live result metadata.</summary>
    public static async Task<CodeResult> GenerateCodeAsync(
        string fileName,
        string sql,
        string connectionString,
        string targetNamespace = "Generated"
    )
    {
        var parsedResult = SqlServerParser.ParseSql(sql: sql);
        if (parsedResult is not ParseOk parsed)
            return parsedResult.Match<CodeResult>(
                _ => new CodeResult.Error<string, SqlError>(new SqlError("Invalid T-SQL")),
                error => new CodeResult.Error<string, SqlError>(new SqlError(error))
            );
        var config = CreateConfig(targetNamespace: targetNamespace);
        var metadata = await config
            .GetColumnMetadata(connectionString, sql, parsed.Value.Parameters)
            .ConfigureAwait(false);
        return metadata is ColumnsOk columns
            ? GenerateFromMetadata(
                fileName: fileName,
                sql: sql,
                parsed: parsed,
                columns: columns,
                config: config
            )
            : metadata.Match<CodeResult>(
                _ => new CodeResult.Error<string, SqlError>(new SqlError("No columns")),
                error => new CodeResult.Error<string, SqlError>(error)
            );
    }

    private static CodeResult GenerateFromMetadata(
        string fileName,
        string sql,
        ParseOk parsed,
        ColumnsOk columns,
        CodeGenerationConfig config
    )
    {
        if (columns.Value.Count > 0)
            return SqlAntlrCodeGenerator.GenerateCodeWithMetadata(
                fileName: fileName,
                sql: sql,
                statement: parsed.Value,
                columnMetadata: columns.Value,
                config: config
            );
        return DataAccessGenerator
            .GenerateNonQueryMethod(
                className: $"{fileName}Extensions",
                methodName: fileName,
                sql: sql,
                parameters: parsed.Value.Parameters.ToImmutableArray(),
                connectionType: config.ConnectionType
            )
            .Match<CodeResult>(
                methods => config.GenerateSourceFile(config.TargetNamespace, string.Empty, methods),
                error => new CodeResult.Error<string, SqlError>(error)
            );
    }

    internal static string MapType(Type fieldType, string dataTypeName, bool isNullable) =>
        (
            dataTypeName.ToUpperInvariant() switch
            {
                "BIT" => "bool",
                "TINYINT" => "byte",
                "SMALLINT" => "short",
                "INT" => "int",
                "BIGINT" => "long",
                "REAL" => "float",
                "FLOAT" => "double",
                "DECIMAL" or "NUMERIC" or "MONEY" or "SMALLMONEY" => "decimal",
                "UNIQUEIDENTIFIER" => "Guid",
                "DATE" or "DATETIME" or "DATETIME2" or "SMALLDATETIME" => "DateTime",
                "DATETIMEOFFSET" => "DateTimeOffset",
                "TIME" => "TimeSpan",
                "BINARY" or "VARBINARY" or "IMAGE" or "TIMESTAMP" => "byte[]",
                _ => fieldType == typeof(string) ? "string" : fieldType.FullName ?? "object",
            }
        ) + (isNullable ? "?" : "");
}
