using Nimblesite.Sql.Model;
using Outcome;

namespace Nimblesite.Lql.SqlServer;

/// <summary>
/// SQL Server-specific extension methods for SelectStatement
/// </summary>
public static class SqlStatementExtensionsSqlServer
{
    /// <summary>
    /// Converts a LqlStatement to SQL Server syntax
    /// </summary>
    /// <param name="statement">The LqlStatement to convert</param>
    /// <returns>A Result containing either SQL Server SQL string or a SqlError</returns>
    public static Result<string, SqlError> ToSqlServer(this LqlStatement statement)
    {
        ArgumentNullException.ThrowIfNull(statement);

        if (LqlStatementValidator.Validate(statement) is SqlError error)
        {
            return new Result<string, SqlError>.Error<string, SqlError>(error);
        }

        try
        {
            // Implements [LQL-CTE] and [LQL-DERIVED-TABLE].
            if (statement.AstNode is { } node && SubqueryLayout.Applies(node))
            {
                return new Result<string, SqlError>.Ok<string, SqlError>(
                    SubqueryLayout.Render(node, SqlServerPaging)
                );
            }

            if (statement.AstNode is Pipeline pipeline)
            {
                var sql = ConvertPipelineToSqlServer(pipeline);
                return new Result<string, SqlError>.Ok<string, SqlError>(sql);
            }

            var unknownSql = statement.AstNode is Identifier identifier
                ? $"SELECT *\nFROM {identifier.Name}"
                : "-- Unknown AST node type";
            return new Result<string, SqlError>.Ok<string, SqlError>(unknownSql);
        }
        catch (Exception ex)
        {
            return new Result<string, SqlError>.Error<string, SqlError>(SqlError.FromException(ex));
        }
    }

    /// <summary>
    /// Converts a pipeline to SQL Server with proper table aliases and column handling
    /// </summary>
    /// <param name="pipeline">The pipeline to convert</param>
    /// <returns>SQL Server SQL string</returns>
    public static string ConvertPipelineToSqlServer(Pipeline pipeline)
    {
        var context = new SqlServerContext();
        return PipelineProcessor.ConvertPipelineToSql(pipeline, context);
    }

    /// <summary>
    /// Converts a Nimblesite.Sql.Model.SelectStatement to SQL Server syntax.
    /// Implements [DP-SQL-MODEL-DIALECTS].
    /// </summary>
    /// <param name="statement">The SelectStatement to convert</param>
    /// <returns>A Result containing either SQL Server SQL string or a SqlError</returns>
    public static Result<string, SqlError> ToSqlServer(this SelectStatement statement)
    {
        try
        {
            var sql = SqlServerContext.ToSqlServerSql(statement);
            return new Result<string, SqlError>.Ok<string, SqlError>(sql);
        }
        catch (Exception ex)
        {
            return new Result<string, SqlError>.Error<string, SqlError>(SqlError.FromException(ex));
        }
    }

    /// <summary>
    /// SQL Server paging: TOP for a bare limit, OFFSET/FETCH once an offset is involved.
    /// </summary>
    private static SqlPaging SqlServerPaging(string? limit, string? offset) =>
        offset is null
            ? new SqlPaging(limit is null ? "" : $"TOP {limit} ", [])
            : new SqlPaging(
                "",
                [
                    $"OFFSET {offset} ROWS",
                    .. limit is null ? Array.Empty<string>() : [$"FETCH NEXT {limit} ROWS ONLY"],
                ]
            );
}
