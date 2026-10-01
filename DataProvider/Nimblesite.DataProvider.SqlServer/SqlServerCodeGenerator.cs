using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.Data.SqlClient;
using Nimblesite.Sql.Model;
using Outcome;
using SqlError = Nimblesite.Sql.Model.SqlError;

namespace Nimblesite.DataProvider.SqlServer;

/// <summary>
/// SQL Server specific code generator static methods
/// </summary>
internal static class SqlServerCodeGenerator
{
    /// <summary>
    /// Generate C# source code for a SQL file (fallback method)
    /// </summary>
    /// <param name="fileName">The name of the SQL file.</param>
    /// <param name="sql">The SQL content.</param>
    /// <param name="statement">The parsed SQL statement metadata.</param>
    /// <param name="_">Whether a custom implementation exists (unused).</param>
    /// <param name="__">Optional grouping configuration for parent-child relationships (unused).</param>
    /// <returns>Result with generated C# source code or error</returns>
    public static Result<string, SqlError> GenerateCode(
        string fileName,
        string sql,
        SelectStatement statement,
        bool _ = false,
        GroupingConfig? __ = null
    )
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return new Result<string, SqlError>.Error<string, SqlError>(
                SqlError.Create("fileName cannot be null or empty")
            );

        if (string.IsNullOrWhiteSpace(sql))
            return new Result<string, SqlError>.Error<string, SqlError>(
                SqlError.Create("sql cannot be null or empty")
            );

        if (statement == null)
            return new Result<string, SqlError>.Error<string, SqlError>(
                SqlError.Create("statement cannot be null")
            );

        // This is the fallback method - should not be used when database metadata is available
        return new Result<string, SqlError>.Error<string, SqlError>(
            SqlError.Create(
                "Use GenerateCodeWithMetadata instead for proper database-driven code generation"
            )
        );
    }

    /// <summary>
    /// Generate C# source code for a SQL file with real database metadata
    /// </summary>
    /// <param name="fileName">The name of the SQL file.</param>
    /// <param name="sql">The SQL content.</param>
    /// <param name="statement">The parsed SQL statement metadata.</param>
    /// <param name="connectionString">Database connection string.</param>
    /// <param name="columnMetadata">Real column metadata from database.</param>
    /// <param name="_">Whether a custom implementation exists (unused).</param>
    /// <param name="groupingConfig">Optional grouping configuration for parent-child relationships.</param>
    /// <returns>Result with generated C# source code or error</returns>
    public static Result<string, SqlError> GenerateCodeWithMetadata(
        string fileName,
        string sql,
        SelectStatement statement,
        string connectionString,
        IReadOnlyList<DatabaseColumn> columnMetadata,
        bool _ = false,
        GroupingConfig? groupingConfig = null
    )
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return new Result<string, SqlError>.Error<string, SqlError>(
                SqlError.Create("fileName cannot be null or empty")
            );

        if (string.IsNullOrWhiteSpace(sql))
            return new Result<string, SqlError>.Error<string, SqlError>(
                SqlError.Create("sql cannot be null or empty")
            );

        if (statement == null)
            return new Result<string, SqlError>.Error<string, SqlError>(
                SqlError.Create("statement cannot be null")
            );

        if (string.IsNullOrWhiteSpace(connectionString))
            return new Result<string, SqlError>.Error<string, SqlError>(
                SqlError.Create("connectionString cannot be null or empty")
            );

        if (columnMetadata == null)
            return new Result<string, SqlError>.Error<string, SqlError>(
                SqlError.Create("columnMetadata cannot be null")
            );

        try
        {
            // If grouping is configured, generate grouped version
            if (groupingConfig != null)
            {
                return GenerateGroupedVersionWithMetadata(
                    fileName,
                    sql,
                    statement,
                    columnMetadata,
                    groupingConfig
                );
            }

            var className = string.Create(CultureInfo.InvariantCulture, $"{fileName}Extensions");
            var parameterList = GenerateParameterList(statement.Parameters);

            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Collections.Immutable;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using Microsoft.Data.SqlClient;");
            sb.AppendLine("using Nimblesite.DataProvider.Core.Dependencies;");
            sb.AppendLine();
            sb.AppendLine("namespace Generated;");
            sb.AppendLine();

            // Generate extension class
            sb.AppendLine(CultureInfo.InvariantCulture, $"public static partial class {className}");
            sb.AppendLine("{");
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"    public static async Task<Result<ImmutableList<{fileName}>, SqlError>> {fileName}Async(this SqlConnection connection, {parameterList})"
            );
            sb.AppendLine("    {");
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"        const string sql = @\"{sql.Replace("\"", "\"\"", StringComparison.Ordinal)}\";"
            );
            sb.AppendLine();
            sb.AppendLine("        try");
            sb.AppendLine("        {");
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"            var results = ImmutableList.CreateBuilder<{fileName}>();"
            );
            sb.AppendLine();
            sb.AppendLine("            using (var command = new SqlCommand(sql, connection))");
            sb.AppendLine("            {");

            // Add parameters
            foreach (var parameter in statement.Parameters)
            {
                sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"                command.Parameters.AddWithValue(\"@{parameter.Name}\", {parameter.Name});"
                );
            }

            sb.AppendLine();
            sb.AppendLine(
                "                using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))"
            );
            sb.AppendLine("                {");
            sb.AppendLine(
                "                    while (await reader.ReadAsync().ConfigureAwait(false))"
            );
            sb.AppendLine("                    {");

            // Generate record constructor using real column metadata
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"                        var item = new {fileName}("
            );

            for (int i = 0; i < columnMetadata.Count; i++)
            {
                var column = columnMetadata[i];
                var isLast = i == columnMetadata.Count - 1;
                var comma = isLast ? "" : ",";

                sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"                            reader.IsDBNull({i}) ? ({column.CSharpType}){(column.IsNullable ? "null" : "default")} : ({column.CSharpType})reader.GetValue({i}){comma}"
                );
            }

            sb.AppendLine("                        );");
            sb.AppendLine("                        results.Add(item);");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"            return new Result<ImmutableList<{fileName}>, SqlError>.Ok(results.ToImmutable());"
            );
            sb.AppendLine("        }");
            sb.AppendLine("        catch (Exception ex)");
            sb.AppendLine("        {");
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"            return new Result<ImmutableList<{fileName}>, SqlError>.Error(new SqlError(\"Database error\", ex));"
            );
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();

            // Generate record based on actual database columns
            sb.AppendLine(CultureInfo.InvariantCulture, $"public record {fileName}(");
            for (int i = 0; i < columnMetadata.Count; i++)
            {
                var column = columnMetadata[i];
                var isLast = i == columnMetadata.Count - 1;
                var comma = isLast ? "" : ",";

                sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"    {column.CSharpType} {column.Name}{comma}"
                );
            }
            sb.AppendLine(");");

            return new Result<string, SqlError>.Ok<string, SqlError>(sb.ToString());
        }
        catch (Exception ex)
        {
            return new Result<string, SqlError>.Error<string, SqlError>(SqlError.FromException(ex));
        }
    }

    /// <summary>
    /// Generate C# source code for Insert/Update operations based on table configuration
    /// </summary>
    /// <param name="table">The database table metadata</param>
    /// <param name="config">The table configuration</param>
    /// <returns>Result with generated C# source code or error</returns>
    public static Result<string, SqlError> GenerateTableOperations(
        DatabaseTable table,
        TableConfig config
    )
    {
        if (table == null)
            return new Result<string, SqlError>.Error<string, SqlError>(
                SqlError.Create("table cannot be null")
            );

        if (config == null)
            return new Result<string, SqlError>.Error<string, SqlError>(
                SqlError.Create("config cannot be null")
            );

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Collections.Immutable;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using Microsoft.Data.SqlClient;");
            sb.AppendLine("using Nimblesite.DataProvider.Core.Dependencies;");
            sb.AppendLine();
            sb.AppendLine("namespace Generated");
            sb.AppendLine("{");

            var className = string.Create(CultureInfo.InvariantCulture, $"{table.Name}Extensions");
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"    public static partial class {className}"
            );
            sb.AppendLine("    {");

            if (config.GenerateInsert)
            {
                GenerateInsertMethod(sb, table);
            }

            if (config.GenerateUpdate)
            {
                GenerateUpdateMethod(sb, table);
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return new Result<string, SqlError>.Ok<string, SqlError>(sb.ToString());
        }
        catch (Exception ex)
        {
            return new Result<string, SqlError>.Error<string, SqlError>(SqlError.FromException(ex));
        }
    }

    private static void GenerateInsertMethod(StringBuilder sb, DatabaseTable table)
    {
        var insertableColumns = table.InsertableColumns;
        if (insertableColumns.Count == 0)
        {
            return;
        }

        var parameterList = string.Join(
            ", ",
            insertableColumns.Select(c =>
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{c.CSharpType} {c.Name.ToLowerInvariant()}"
                )
            )
        );

        sb.AppendLine();
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"        public static async Task<Result<int, SqlError>> Insert{table.Name}Async(this SqlConnection connection, {parameterList})"
        );
        sb.AppendLine("        {");

        // Generate INSERT SQL
        var columnNames = string.Join(", ", insertableColumns.Select(c => c.Name));
        var parameterNames = string.Join(", ", insertableColumns.Select(c => $"@{c.Name}"));

        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            const string sql = \"INSERT INTO [{table.Schema}].[{table.Name}] ({columnNames}) VALUES ({parameterNames}); SELECT CAST(SCOPE_IDENTITY() AS int)\";"
        );
        sb.AppendLine();
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                using (var command = new SqlCommand(sql, connection))");
        sb.AppendLine("                {");

        // Add parameters
        foreach (var column in insertableColumns)
        {
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"                    command.Parameters.AddWithValue(\"@{column.Name}\", {column.Name.ToLowerInvariant()});"
            );
        }

        sb.AppendLine();
        sb.AppendLine(
            "                    var result = await command.ExecuteScalarAsync().ConfigureAwait(false);"
        );
        sb.AppendLine(
            "                    var newId = Convert.ToInt32(result, CultureInfo.InvariantCulture);"
        );
        sb.AppendLine(
            "                    return new Result<int, SqlError>.Ok<int, SqlError>(newId);"
        );
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            catch (Exception ex)");
        sb.AppendLine("            {");
        sb.AppendLine(
            "                return new Result<int, SqlError>.Error<int, SqlError>(new SqlError(\"Insert failed\", ex));"
        );
        sb.AppendLine("            }");
        sb.AppendLine("        }");
    }

    private static void GenerateUpdateMethod(StringBuilder sb, DatabaseTable table)
    {
        var updateableColumns = table.UpdateableColumns;
        var primaryKeyColumns = table.PrimaryKeyColumns;

        if (updateableColumns.Count == 0 || primaryKeyColumns.Count == 0)
        {
            return;
        }

        var allColumns = primaryKeyColumns.Concat(updateableColumns).ToList();
        var parameterList = string.Join(
            ", ",
            allColumns.Select(c =>
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{c.CSharpType} {c.Name.ToLowerInvariant()}"
                )
            )
        );

        sb.AppendLine();
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"        public static async Task<Result<int, SqlError>> Update{table.Name}Async(this SqlConnection connection, {parameterList})"
        );
        sb.AppendLine("        {");

        // Generate UPDATE SQL
        var setClause = string.Join(", ", updateableColumns.Select(c => $"{c.Name} = @{c.Name}"));
        var whereClause = string.Join(
            " AND ",
            primaryKeyColumns.Select(c => $"{c.Name} = @{c.Name}")
        );

        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            const string sql = \"UPDATE [{table.Schema}].[{table.Name}] SET {setClause} WHERE {whereClause}\";"
        );
        sb.AppendLine();
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                using (var command = new SqlCommand(sql, connection))");
        sb.AppendLine("                {");

        // Add parameters
        foreach (var column in allColumns)
        {
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"                    command.Parameters.AddWithValue(\"@{column.Name}\", {column.Name.ToLowerInvariant()});"
            );
        }

        sb.AppendLine();
        sb.AppendLine(
            "                    var rowsAffected = await command.ExecuteNonQueryAsync().ConfigureAwait(false);"
        );
        sb.AppendLine(
            "                    return new Result<int, SqlError>.Ok<int, SqlError>(rowsAffected);"
        );
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            catch (Exception ex)");
        sb.AppendLine("            {");
        sb.AppendLine(
            "                return new Result<int, SqlError>.Error<int, SqlError>(new SqlError(\"Update failed\", ex));"
        );
        sb.AppendLine("            }");
        sb.AppendLine("        }");
    }

    /// <summary>
    /// Builds a comma-separated parameter list for method signatures.
    /// </summary>
    /// <param name="parameters">The parameters discovered in the SQL statement.</param>
    /// <returns>A formatted parameter list for method signatures.</returns>
    private static string GenerateParameterList(IReadOnlyList<ParameterInfo> parameters)
    {
        if (parameters.Count == 0)
        {
            return "";
        }

        return string.Join(", ", parameters.Select(p => $"object {p.Name}"));
    }

    /// <summary>
    /// Generates grouped version when grouping configuration is provided.
    /// </summary>
    /// <param name="fileName">The SQL file name.</param>
    /// <param name="sql">The SQL content.</param>
    /// <param name="statement">The parsed SQL statement.</param>
    /// <param name="columnMetadata">Real column metadata from database.</param>
    /// <param name="groupingConfig">The grouping configuration.</param>
    private static Result<string, SqlError> GenerateGroupedVersionWithMetadata(
        string fileName,
        string sql,
        SelectStatement statement,
        IReadOnlyList<DatabaseColumn> columnMetadata,
        GroupingConfig groupingConfig
    )
    {
        var className = string.Create(CultureInfo.InvariantCulture, $"{fileName}Extensions");
        var parameterList = GenerateParameterList(statement.Parameters);

        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Collections.Immutable;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using Microsoft.Data.SqlClient;");
        sb.AppendLine("using Nimblesite.DataProvider.Core.Dependencies;");
        sb.AppendLine();
        sb.AppendLine("namespace Generated;");
        sb.AppendLine();

        // Generate extension class
        sb.AppendLine(CultureInfo.InvariantCulture, $"public static partial class {className}");
        sb.AppendLine("{");

        // Generate method that returns grouped results
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    public static async Task<Result<IReadOnlyList<{groupingConfig.ParentEntity.Name}WithChildren>, SqlError>> {fileName}Async(this SqlConnection connection, {parameterList})"
        );
        sb.AppendLine("    {");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"        const string sql = @\"{sql.Replace("\"", "\"\"", StringComparison.Ordinal)}\";"
        );
        sb.AppendLine();
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            var rawResults = ImmutableList.CreateBuilder<{fileName}Raw>();"
        );
        sb.AppendLine();
        sb.AppendLine("            using (var command = new SqlCommand(sql, connection))");
        sb.AppendLine("            {");

        // Add parameters
        foreach (var parameter in statement.Parameters)
        {
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"                command.Parameters.AddWithValue(\"@{parameter.Name}\", {parameter.Name});"
            );
        }

        sb.AppendLine();
        sb.AppendLine(
            "                using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))"
        );
        sb.AppendLine("                {");
        sb.AppendLine("                    while (await reader.ReadAsync().ConfigureAwait(false))");
        sb.AppendLine("                    {");

        // Generate raw record constructor using real column metadata
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"                        var item = new {fileName}Raw("
        );

        for (int i = 0; i < columnMetadata.Count; i++)
        {
            var column = columnMetadata[i];
            var isLast = i == columnMetadata.Count - 1;
            var comma = isLast ? "" : ",";

            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"                            reader.IsDBNull({i}) ? ({column.CSharpType}){(column.IsNullable ? "null" : "default")} : ({column.CSharpType})reader.GetValue({i}){comma}"
            );
        }

        sb.AppendLine("                        );");
        sb.AppendLine("                        rawResults.Add(item);");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            // Group the raw results into parent-child structure");
        sb.AppendLine(
            "            var grouped = GroupResults(rawResults.ToImmutable(), groupingConfig);"
        );
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            return new Result<IReadOnlyList<{groupingConfig.ParentEntity.Name}WithChildren>, SqlError>.Ok(grouped);"
        );
        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            return new Result<IReadOnlyList<{groupingConfig.ParentEntity.Name}WithChildren>, SqlError>.Error(new SqlError(\"Database error\", ex));"
        );
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Generate grouping method
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    private static IReadOnlyList<{groupingConfig.ParentEntity.Name}WithChildren> GroupResults(ImmutableList<{fileName}Raw> rawResults, GroupingConfig groupingConfig)"
        );
        sb.AppendLine("    {");
        sb.AppendLine("        // Group raw results by parent key columns");
        sb.AppendLine("        var parentGroups = rawResults");
        sb.AppendLine("            .GroupBy(r => new { ");

        // Generate grouping key based on parent key columns
        for (int i = 0; i < groupingConfig.ParentEntity.KeyColumns.Count; i++)
        {
            var keyColumn = groupingConfig.ParentEntity.KeyColumns[i];
            var isLast = i == groupingConfig.ParentEntity.KeyColumns.Count - 1;
            var comma = isLast ? "" : ",";
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"                {keyColumn} = r.{keyColumn}{comma}"
            );
        }

        sb.AppendLine("            })");
        sb.AppendLine("            .ToList();");
        sb.AppendLine();
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"        var result = new List<{groupingConfig.ParentEntity.Name}WithChildren>();"
        );
        sb.AppendLine("        foreach (var group in parentGroups)");
        sb.AppendLine("        {");
        sb.AppendLine("            var firstItem = group.First();");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            var parent = new {groupingConfig.ParentEntity.Name}WithChildren("
        );

        // Generate parent properties
        for (int i = 0; i < groupingConfig.ParentEntity.Columns.Count; i++)
        {
            var column = groupingConfig.ParentEntity.Columns[i];
            var isLast = i == groupingConfig.ParentEntity.Columns.Count - 1;
            var comma = isLast ? "" : ",";
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"                firstItem.{column}{comma}"
            );
        }
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"                group.Select(item => new {groupingConfig.ChildEntity.Name}("
        );

        // Generate child properties
        for (int i = 0; i < groupingConfig.ChildEntity.Columns.Count; i++)
        {
            var column = groupingConfig.ChildEntity.Columns[i];
            var isLast = i == groupingConfig.ChildEntity.Columns.Count - 1;
            var comma = isLast ? "" : ",";
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"                    item.{column}{comma}"
            );
        }
        sb.AppendLine("                )).ToList()");
        sb.AppendLine("            );");
        sb.AppendLine("            result.Add(parent);");
        sb.AppendLine("        }");
        sb.AppendLine("        return result;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Generate raw data record using real column metadata
        sb.AppendLine(CultureInfo.InvariantCulture, $"internal record {fileName}Raw(");
        for (int i = 0; i < columnMetadata.Count; i++)
        {
            var column = columnMetadata[i];
            var isLast = i == columnMetadata.Count - 1;
            var comma = isLast ? "" : ",";

            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"    {column.CSharpType} {column.Name}{comma}"
            );
        }
        sb.AppendLine(");");
        sb.AppendLine();

        // Generate parent entity with children
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"public record {groupingConfig.ParentEntity.Name}WithChildren("
        );
        for (int i = 0; i < groupingConfig.ParentEntity.Columns.Count; i++)
        {
            var column = groupingConfig.ParentEntity.Columns[i];
            var columnMetadataItem = columnMetadata.FirstOrDefault(c => c.Name == column);
            var csharpType = columnMetadataItem?.CSharpType ?? "object";
            sb.AppendLine(CultureInfo.InvariantCulture, $"    {csharpType} {column},");
        }
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    IReadOnlyList<{groupingConfig.ChildEntity.Name}> Children"
        );
        sb.AppendLine(");");
        sb.AppendLine();

        // Generate child entity
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"public record {groupingConfig.ChildEntity.Name}("
        );
        for (int i = 0; i < groupingConfig.ChildEntity.Columns.Count; i++)
        {
            var column = groupingConfig.ChildEntity.Columns[i];
            var columnMetadataItem = columnMetadata.FirstOrDefault(c => c.Name == column);
            var csharpType = columnMetadataItem?.CSharpType ?? "object";
            var isLast = i == groupingConfig.ChildEntity.Columns.Count - 1;
            var comma = isLast ? "" : ",";
            sb.AppendLine(CultureInfo.InvariantCulture, $"    {csharpType} {column}{comma}");
        }
        sb.AppendLine(");");

        return new Result<string, SqlError>.Ok<string, SqlError>(sb.ToString());
    }

    /// <summary>
    /// Gets column metadata by executing the SQL query against the database.
    /// This is the proper way to get column types - by executing the query and checking metadata.
    /// </summary>
    /// <param name="connectionString">Database connection string</param>
    /// <param name="sql">SQL query to execute</param>
    /// <param name="parameters">Query parameters</param>
    /// <returns>Result with list of database columns with their metadata or error</returns>
    public static async Task<
        Result<IReadOnlyList<DatabaseColumn>, SqlError>
    > GetColumnMetadataFromSqlAsync(
        string connectionString,
        string sql,
        IEnumerable<ParameterInfo> parameters
    )
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return new Result<IReadOnlyList<DatabaseColumn>, SqlError>.Error(
                SqlError.Create("connectionString cannot be null or empty")
            );

        if (string.IsNullOrWhiteSpace(sql))
            return new Result<IReadOnlyList<DatabaseColumn>, SqlError>.Error(
                SqlError.Create("sql cannot be null or empty")
            );

        if (parameters == null)
            return new Result<IReadOnlyList<DatabaseColumn>, SqlError>.Error(
                SqlError.Create("parameters cannot be null")
            );

        var columns = new List<DatabaseColumn>();

        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            // The SQL is provided by the developer at compile time, not user input
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
            using var command = new SqlCommand(sql, connection);
#pragma warning restore CA2100 // Review SQL queries for security vulnerabilities

            // Add parameters with dummy values to get schema
            foreach (var param in parameters)
            {
                var dummyValue = GetDummyValueForParameter(param);
                command.Parameters.AddWithValue($"@{param.Name}", dummyValue);
            }

            using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);

            // Get column schema from the reader
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var columnName = reader.GetName(i);
                var fieldType = reader.GetFieldType(i);
                var dataTypeName = reader.GetDataTypeName(i);

                var csharpType = MapSqlServerTypeToCSharpType(fieldType);

                columns.Add(
                    new DatabaseColumn
                    {
                        Name = columnName,
                        SqlType = dataTypeName,
                        CSharpType = csharpType,
                        IsNullable =
                            !fieldType.IsValueType || Nullable.GetUnderlyingType(fieldType) != null,
                        IsPrimaryKey = false, // Cannot determine from query result
                        IsIdentity = false, // Cannot determine from query result
                        IsComputed = false, // Cannot determine from query result
                    }
                );
            }
        }
        catch (SqlException ex)
        {
            // If we can't execute the query, return error
            return new Result<IReadOnlyList<DatabaseColumn>, SqlError>.Error(
                SqlError.FromException(ex)
            );
        }
        catch (Exception ex)
        {
            return new Result<IReadOnlyList<DatabaseColumn>, SqlError>.Error(
                SqlError.FromException(ex)
            );
        }

        return new Result<IReadOnlyList<DatabaseColumn>, SqlError>.Ok(columns.AsReadOnly());
    }

    /// <summary>
    /// Maps SQL Server data types to C# types
    /// </summary>
    /// <param name="fieldType">The .NET field type</param>
    /// <returns>C# type name</returns>
    private static string MapSqlServerTypeToCSharpType(Type fieldType)
    {
        // Use the .NET type first as it's more accurate
        if (fieldType == typeof(int) || fieldType == typeof(int?))
            return fieldType == typeof(int?) ? "int?" : "int";

        if (fieldType == typeof(long) || fieldType == typeof(long?))
            return fieldType == typeof(long?) ? "long?" : "long";

        if (fieldType == typeof(short) || fieldType == typeof(short?))
            return fieldType == typeof(short?) ? "short?" : "short";

        if (fieldType == typeof(byte) || fieldType == typeof(byte?))
            return fieldType == typeof(byte?) ? "byte?" : "byte";

        if (fieldType == typeof(double) || fieldType == typeof(double?))
            return fieldType == typeof(double?) ? "double?" : "double";

        if (fieldType == typeof(float) || fieldType == typeof(float?))
            return fieldType == typeof(float?) ? "float?" : "float";

        if (fieldType == typeof(decimal) || fieldType == typeof(decimal?))
            return fieldType == typeof(decimal?) ? "decimal?" : "decimal";

        if (fieldType == typeof(bool) || fieldType == typeof(bool?))
            return fieldType == typeof(bool?) ? "bool?" : "bool";

        if (fieldType == typeof(DateTime) || fieldType == typeof(DateTime?))
            return fieldType == typeof(DateTime?) ? "DateTime?" : "DateTime";

        if (fieldType == typeof(DateOnly) || fieldType == typeof(DateOnly?))
            return fieldType == typeof(DateOnly?) ? "DateOnly?" : "DateOnly";

        if (fieldType == typeof(TimeOnly) || fieldType == typeof(TimeOnly?))
            return fieldType == typeof(TimeOnly?) ? "TimeOnly?" : "TimeOnly";

        if (fieldType == typeof(DateTimeOffset) || fieldType == typeof(DateTimeOffset?))
            return fieldType == typeof(DateTimeOffset?) ? "DateTimeOffset?" : "DateTimeOffset";

        if (fieldType == typeof(string))
            return "string";

        if (fieldType == typeof(byte[]))
            return "byte[]";

        if (fieldType == typeof(Guid) || fieldType == typeof(Guid?))
            return fieldType == typeof(Guid?) ? "Guid?" : "Guid";

        // Fall back to string for unknown types
        return "string";
    }

    /// <summary>
    /// Gets a dummy value for a parameter based on its name for schema discovery
    /// </summary>
    /// <param name="parameter">Parameter info</param>
    /// <returns>Dummy value</returns>
    private static object GetDummyValueForParameter(ParameterInfo parameter)
    {
        var lowerName = parameter.Name.ToLowerInvariant();

        return lowerName switch
        {
            var name when name.Contains("id", StringComparison.Ordinal) => 1,
            var name when name.Contains("count", StringComparison.Ordinal) => 1,
            var name when name.Contains("quantity", StringComparison.Ordinal) => 1,
            var name when name.Contains("amount", StringComparison.Ordinal) => 1.0m,
            var name when name.Contains("price", StringComparison.Ordinal) => 1.0m,
            var name when name.Contains("total", StringComparison.Ordinal) => 1.0m,
            var name when name.Contains("percentage", StringComparison.Ordinal) => 1.0m,
            var name when name.Contains("date", StringComparison.Ordinal) => DateTime.UtcNow,
            var name when name.Contains("time", StringComparison.Ordinal) => DateTime.UtcNow,
            var name when name.Contains("created", StringComparison.Ordinal) => DateTime.UtcNow,
            var name when name.Contains("updated", StringComparison.Ordinal) => DateTime.UtcNow,
            _ => "dummy_value",
        };
    }
}
