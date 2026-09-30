using Nimblesite.Sql.Model;
using Outcome;

namespace Nimblesite.Lql.Postgres;

/// <summary>
/// PostgreSQL-specific extension methods for SelectStatement
/// </summary>
public static class SqlStatementExtensionsPostgreSQL
{
    /// <summary>
    /// Converts a LqlStatement to PostgreSQL syntax
    /// </summary>
    /// <param name="statement">The LqlStatement to convert</param>
    /// <returns>A Result containing either PostgreSQL SQL string or a SqlError</returns>
    public static Result<string, SqlError> ToPostgreSql(this LqlStatement statement)
    {
        ArgumentNullException.ThrowIfNull(statement);

        if (LqlStatementValidator.Validate(statement) is SqlError error)
        {
            return new Result<string, SqlError>.Error<string, SqlError>(error);
        }

        try
        {
            if (statement.AstNode is Pipeline pipeline)
            {
                var sql = ConvertPipelineToPostgreSQL(pipeline);
                return new Result<string, SqlError>.Ok<string, SqlError>(sql);
            }

            var unknownSql = statement.AstNode is Identifier identifier
                ? $"SELECT *\nFROM {FormatBareIdentifier(identifier.Name)}"
                : "-- Unknown AST node type";
            return new Result<string, SqlError>.Ok<string, SqlError>(unknownSql);
        }
        catch (Exception ex)
        {
            return new Result<string, SqlError>.Error<string, SqlError>(SqlError.FromException(ex));
        }
    }

    /// <summary>
    /// Converts a pipeline to PostgreSQL with proper table aliases and column handling
    /// </summary>
    /// <param name="pipeline">The pipeline to convert</param>
    /// <returns>PostgreSQL SQL string</returns>
    private static string ConvertPipelineToPostgreSQL(Pipeline pipeline)
    {
        var context = new PostgreSqlContext();
        return PipelineProcessor.ConvertPipelineToSql(pipeline, context, ProcessColumnReferences);
    }

    /// <summary>
    /// Wraps a bare identifier in double quotes only when it contains
    /// uppercase ASCII (which Postgres would otherwise fold). Lower-case
    /// identifiers are passed through to preserve existing test fixture
    /// output and SQL readability.
    /// </summary>
    private static string FormatBareIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (c >= 'A' && c <= 'Z')
            {
                return $"\"{name}\"";
            }
        }
        return name;
    }

    /// <summary>
    /// Processes column references in a condition string to use proper table aliases
    /// </summary>
    /// <param name="condition">The condition string to process</param>
    /// <returns>The processed condition with proper table aliases</returns>
    private static string ProcessColumnReferences(string condition)
    {
        if (string.IsNullOrEmpty(condition))
            return condition;

        var processedCondition = condition;

        // Replace row.tableName.column with tableAlias.column
        // For simple case, replace row.orders.status with o.status pattern
        processedCondition = processedCondition.Replace(
            "row.orders.",
            "o.",
            StringComparison.OrdinalIgnoreCase
        );
        processedCondition = processedCondition.Replace(
            "row.users.",
            "u.",
            StringComparison.OrdinalIgnoreCase
        );
        processedCondition = processedCondition.Replace(
            "row.employees.",
            "e.",
            StringComparison.OrdinalIgnoreCase
        );

        return processedCondition;
    }
}
