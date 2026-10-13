using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Nimblesite.Sql.Model;

namespace Nimblesite.DataProvider.Core.CodeGeneration;

// Implements [DP-CODEGEN-SHARED-SQL-METHODS].
public static partial class DataAccessGenerator
{
    private static void AppendSqlHeader(
        StringBuilder builder,
        string className,
        string methodName,
        string connectionType,
        IReadOnlyList<ParameterInfo>? parameters,
        string resultType,
        bool query
    )
    {
        ImmutableArray<string> lines =
        [
            "/// <summary>",
            $"/// Extension methods for '{methodName}'.",
            "/// </summary>",
            $"public static partial class {className}",
            "{",
            "    /// <summary>",
            $"    /// Executes '{methodName}.sql' and {(query ? "maps results" : "returns rows affected")}.",
            "    /// </summary>",
            $"    /// <param name=\"connection\">Open {connectionType} connection.</param>",
        ];
        foreach (var line in lines)
            builder.AppendLine(line);
        foreach (var parameter in parameters ?? [])
            builder.AppendLine(
                CultureInfo.InvariantCulture,
                $"    /// <param name=\"{parameter.Name}\">Query parameter.</param>"
            );
        builder.AppendLine(
            query
                ? "    /// <returns>Result of records or SQL error.</returns>"
                : "    /// <returns>Result with rows affected or SQL error.</returns>"
        );
        var parameterList = GenerateParameterList(parameters: parameters ?? []);
        builder.AppendLine(
            CultureInfo.InvariantCulture,
            $"    public static async Task<Result<{resultType}, SqlError>> {methodName}Async(this {connectionType} connection{(string.IsNullOrEmpty(parameterList) ? "" : ", " + parameterList)})"
        );
    }

    private static void AppendSqlStart(StringBuilder builder, string sql)
    {
        builder.AppendLine("    {");
        builder.AppendLine(
            CultureInfo.InvariantCulture,
            $"        const string sql = @\"{sql.Replace("\"", "\"\"", StringComparison.Ordinal)}\";"
        );
        global::Nimblesite.DataProvider.Core.CodeGeneration.ModelGenerator.AppendSourceLines(
            builder: builder,
            lines: EmissionBlockLines12
        );
    }

    private static void AppendNativeCommand(StringBuilder builder, string connectionType)
    {
        var commandType = connectionType.Replace("Connection", "Command", StringComparison.Ordinal);
        builder.AppendLine(
            CultureInfo.InvariantCulture,
            $"            using (var command = new {commandType}(sql, connection))"
        );
        builder.AppendLine("            {");
    }

    private static void AppendSqlParameters(
        StringBuilder builder,
        string connectionType,
        IReadOnlyList<ParameterInfo>? parameters,
        IReadOnlyList<DatabaseColumn>? columns = null
    )
    {
        foreach (var parameter in parameters ?? [])
        {
            if (columns is not null && connectionType.Contains("Npgsql", StringComparison.Ordinal))
            {
                AppendTypedSqlParameter(builder: builder, name: parameter.Name, columns: columns);
            }
            else
            {
                builder.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"                command.Parameters.AddWithValue(\"@{parameter.Name}\", {parameter.Name} ?? (object)DBNull.Value);"
                );
            }
        }
    }

    private static void AppendTypedSqlParameter(
        StringBuilder builder,
        string name,
        IReadOnlyList<DatabaseColumn> columns
    )
    {
        var type = ResolveNpgsqlDbTypeFromColumns(parameterName: name, columns: columns);
        builder.AppendLine(
            CultureInfo.InvariantCulture,
            $"                if ({name} is not null and not DBNull)"
        );
        builder.AppendLine(
            CultureInfo.InvariantCulture,
            $"                    command.Parameters.AddWithValue(\"@{name}\", {name});"
        );
        builder.AppendLine("                else");
        builder.AppendLine(
            CultureInfo.InvariantCulture,
            $"                    command.Parameters.Add(new NpgsqlParameter(\"@{name}\", NpgsqlTypes.NpgsqlDbType.{type}) {{ Value = DBNull.Value }});"
        );
    }
}
