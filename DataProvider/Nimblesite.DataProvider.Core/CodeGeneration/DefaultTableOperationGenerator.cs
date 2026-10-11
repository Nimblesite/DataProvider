using System.Globalization;
using System.Text;
using Nimblesite.Sql.Model;
using Outcome;

namespace Nimblesite.DataProvider.Core.CodeGeneration;

/// <summary>
/// Default implementation for generating table operations
/// </summary>
public class DefaultTableOperationGenerator : ITableOperationGenerator
{
    private readonly string _connectionType;

    /// <summary>
    /// Initializes a new instance of DefaultTableOperationGenerator
    /// </summary>
    /// <param name="connectionType">Database connection type (e.g., SqliteConnection)</param>
    public DefaultTableOperationGenerator(string connectionType = "SqliteConnection")
    {
        _connectionType = connectionType;
    }

    /// <summary>
    /// Generates INSERT and UPDATE operations for a database table
    /// </summary>
    public virtual Result<string, SqlError> GenerateTableOperations(
        DatabaseTable table,
        TableConfig config
    )
    {
        if (table == null)
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("table cannot be null")
            );

        if (config == null)
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("config cannot be null")
            );

        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Collections.Immutable;");
        sb.AppendLine("using System.Data;");
        sb.AppendLine("using System.Globalization;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine(CultureInfo.InvariantCulture, $"using {GetConnectionNamespace()};");
        sb.AppendLine("using Outcome;");
        sb.AppendLine("using Nimblesite.Sql.Model;");
        if (GetConnectionNamespace() == "Microsoft.Data.SqlClient")
            sb.AppendLine("using SqlError = Nimblesite.Sql.Model.SqlError;");
        sb.AppendLine();
        sb.AppendLine("namespace Generated");
        sb.AppendLine("{");

        var className = string.Create(CultureInfo.InvariantCulture, $"{table.Name}Extensions");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// Extension methods for table operations on {table.Name}"
        );
        sb.AppendLine("    /// </summary>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    public static partial class {className}");
        sb.AppendLine("    {");

        if (config.GenerateInsert)
        {
            var insertResult = GenerateInsertMethod(table);
            if (insertResult is Result<string, SqlError>.Ok<string, SqlError> insertSuccess)
            {
                sb.AppendLine(insertSuccess.Value);
            }
            else if (insertResult is Result<string, SqlError>.Error<string, SqlError> insertFailure)
            {
                return insertFailure;
            }
        }

        if (config.GenerateUpdate)
        {
            var updateResult = GenerateUpdateMethod(table);
            if (updateResult is Result<string, SqlError>.Ok<string, SqlError> updateSuccess)
            {
                sb.AppendLine(updateSuccess.Value);
            }
            else if (updateResult is Result<string, SqlError>.Error<string, SqlError> updateFailure)
            {
                return updateFailure;
            }
        }

        if (config.GenerateDelete)
        {
            var deleteResult = DataAccessGenerator.GenerateDeleteMethod(
                table: table,
                connectionType: _connectionType
            );
            if (deleteResult is Result<string, SqlError>.Error<string, SqlError> failure)
                return failure;
            if (deleteResult is Result<string, SqlError>.Ok<string, SqlError> success)
                sb.AppendLine(success.Value);
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return new Result<string, SqlError>.Ok<string, SqlError>(sb.ToString());
    }

    /// <summary>
    /// Generates an INSERT method for a database table
    /// </summary>
    public virtual Result<string, SqlError> GenerateInsertMethod(DatabaseTable table) =>
        DataAccessGenerator.GenerateInsertMethod(table, _connectionType);

    /// <summary>
    /// Generates an UPDATE method for a database table
    /// </summary>
    public virtual Result<string, SqlError> GenerateUpdateMethod(DatabaseTable table) =>
        DataAccessGenerator.GenerateUpdateMethod(table, _connectionType);

    /// <summary>
    /// Gets the namespace for the connection type
    /// </summary>
    protected virtual string GetConnectionNamespace() =>
        ResolveConnectionNamespace(connectionType: _connectionType);

    internal static string ResolveConnectionNamespace(string connectionType) =>
        connectionType.Split('.').Last() switch
        {
            "SqliteConnection" => "Microsoft.Data.Sqlite",
            "SqlConnection" => "Microsoft.Data.SqlClient",
            "NpgsqlConnection" => "Npgsql",
            _ => "System.Data.Common",
        };
}
