using System.Globalization;
using System.Text;
using Nimblesite.Sql.Model;
using Outcome;

namespace Nimblesite.DataProvider.Core.CodeGeneration;

/// <summary>
/// Pure transformation functions for generating grouped query code
/// </summary>
internal static partial class GroupingTransformations
{
    /// <summary>
    /// Generates the grouping method that transforms flat results into parent-child structure
    /// </summary>
    /// <param name="fileName">Base file name</param>
    /// <param name="groupingConfig">Grouping configuration</param>
    /// <returns>Generated grouping method code</returns>
    public static Result<string, SqlError> GenerateGroupingMethod(
        string fileName,
        GroupingConfig groupingConfig
    )
    {
        if (
            CodeGenerationGuards.ValidateGroupingMethod(
                fileName: fileName,
                groupingConfig: groupingConfig
            ) is
            { } error
        )
            return error;

        var sb = new StringBuilder();

        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    private static ImmutableList<{groupingConfig.ParentEntity.Name}> GroupResults(ImmutableList<{fileName}Raw> rawResults)"
        );
        global::Nimblesite.DataProvider.Core.CodeGeneration.ModelGenerator.AppendSourceLines(
            builder: sb,
            lines: EmissionBlockLines15
        );

        // Generate grouping key based on parent key columns
        ModelGenerator.AppendIndexedLines(
            builder: sb,
            count: groupingConfig.ParentEntity.KeyColumns.Count,
            render: (i, comma) =>
                $"                {groupingConfig.ParentEntity.KeyColumns[i]} = r.{groupingConfig.ParentEntity.KeyColumns[i]}{comma}"
        );

        global::Nimblesite.DataProvider.Core.CodeGeneration.ModelGenerator.AppendSourceLines(
            builder: sb,
            lines: EmissionBlockLines16
        );
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"        var result = new List<{groupingConfig.ParentEntity.Name}>();"
        );
        global::Nimblesite.DataProvider.Core.CodeGeneration.ModelGenerator.AppendSourceLines(
            builder: sb,
            lines: EmissionBlockLines17
        );
        // Generate parent and child constructor arguments
        ModelGenerator.AppendGroupedConstruction(
            builder: sb,
            parentName: groupingConfig.ParentEntity.Name,
            parentColumns: groupingConfig.ParentEntity.Columns,
            childName: groupingConfig.ChildEntity.Name,
            childColumns: groupingConfig.ChildEntity.Columns
        );
        return ModelGenerator.CompleteResult(builder: sb, tail: EmissionBlockLines18);
    }

    /// <summary>
    /// Generates a grouped query method that returns parent-child results
    /// </summary>
    /// <param name="className">Extension class name</param>
    /// <param name="methodName">Method name</param>
    /// <param name="sql">SQL query</param>
    /// <param name="parameters">SQL parameters</param>
    /// <param name="columns">Database columns</param>
    /// <param name="groupingConfig">Grouping configuration</param>
    /// <param name="connectionType">Database connection type</param>
    /// <returns>Generated grouped query method code</returns>
    public static Result<string, SqlError> GenerateGroupedQueryMethod(
        string className,
        string methodName,
        string sql,
        IReadOnlyList<ParameterInfo> parameters,
        IReadOnlyList<DatabaseColumn> columns,
        GroupingConfig groupingConfig,
        string connectionType = "SqliteConnection"
    )
    {
        if (
            CodeGenerationGuards.ValidateGroupedQueryGeneration(
                className: className,
                methodName: methodName,
                sql: sql,
                columns: columns,
                groupingConfig: groupingConfig
            ) is
            { } error
        )
            return error;

        var parameterList = DataAccessGenerator.GenerateParameterList(parameters);
        var sb = new StringBuilder();

        // Generate extension class with XML docs
        ModelGenerator.AppendXmlDocHeader(
            builder: sb,
            summary: $"Extension methods for '{methodName}' grouped query.",
            declaration: $"public static partial class {className}"
        );
        sb.AppendLine("{");

        // Generate method that returns grouped results
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// Executes the '{methodName}' query and groups rows into '{groupingConfig.ParentEntity.Name}' with '{groupingConfig.ChildEntity.Name}' children."
        );
        sb.AppendLine("    /// </summary>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// <param name=\"connection\">The open {connectionType} connection.</param>"
        );

        if (parameters != null && parameters.Count > 0)
        {
            foreach (var p in parameters)
            {
                sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"    /// <param name=\"{p.Name}\">Query parameter.</param>"
                );
            }
        }

        sb.AppendLine("    /// <returns>Result of grouped records or SQL error.</returns>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    public static async Task<Result<ImmutableList<{groupingConfig.ParentEntity.Name}>, SqlError>> {methodName}Async(this {connectionType} connection{(string.IsNullOrEmpty(parameterList) ? "" : ", " + parameterList)})"
        );
        sb.AppendLine("    {");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"        const string sql = @\"{sql.Replace("\"", "\"\"", StringComparison.Ordinal)}\";"
        );
        ModelGenerator.AppendBlockAndLine(
            builder: sb,
            block: EmissionBlockLines19,
            line: $"            var rawResults = ImmutableList.CreateBuilder<{methodName}Raw>();"
        );
        sb.AppendLine();

        var commandType = connectionType.Replace("Connection", "Command", StringComparison.Ordinal);
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            using (var command = new {commandType}(sql, connection))"
        );
        sb.AppendLine("            {");

        // Add parameters
        if (parameters != null)
        {
            foreach (var parameter in parameters)
            {
                sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"                command.Parameters.AddWithValue(\"@{parameter.Name}\", {parameter.Name} ?? (object)DBNull.Value);"
                );
            }
        }

        // Generate raw record constructor using real column metadata
        ModelGenerator.AppendBlockAndLine(
            builder: sb,
            block: EmissionBlockLines20,
            line: $"                        var item = new {methodName}Raw("
        );

        ModelGenerator.AppendIndexedLines(
            builder: sb,
            count: columns.Count,
            render: (i, comma) =>
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"                            reader.IsDBNull({i}) ? ({columns[i].CSharpType}){(columns[i].IsNullable ? "null" : "default")} : ({columns[i].CSharpType})reader.GetValue({i}){comma}"
                )
        );

        ModelGenerator.AppendBlockAndLine(
            builder: sb,
            block: EmissionBlockLines21,
            line: $"            return new Result<ImmutableList<{groupingConfig.ParentEntity.Name}>, SqlError>.Ok<ImmutableList<{groupingConfig.ParentEntity.Name}>, SqlError>(grouped);"
        );
        ModelGenerator.AppendBlockAndLine(
            builder: sb,
            block: EmissionBlockLines22,
            line: $"            return new Result<ImmutableList<{groupingConfig.ParentEntity.Name}>, SqlError>.Error<ImmutableList<{groupingConfig.ParentEntity.Name}>, SqlError>(new SqlError(\"Database error\", ex));"
        );
        global::Nimblesite.DataProvider.Core.CodeGeneration.ModelGenerator.AppendSourceLines(
            builder: sb,
            lines: EmissionBlockLines23
        );
        var groupingMethodResult = GenerateGroupingMethod(methodName, groupingConfig);
        if (groupingMethodResult is Result<string, SqlError>.Ok<string, SqlError> groupingSuccess)
        {
            sb.Append(groupingSuccess.Value);
        }
        else if (
            groupingMethodResult is Result<string, SqlError>.Error<string, SqlError> groupingFailure
        )
        {
            return groupingFailure;
        }

        sb.AppendLine("}");

        return new Result<string, SqlError>.Ok<string, SqlError>(sb.ToString());
    }
}
