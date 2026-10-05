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
    // Implements [LQL-RENDER-SHARED].
    public static Result<string, SqlError> ToSqlServer(this LqlStatement statement) =>
        StatementRendering.Render(
            statement: statement,
            paging: SqlServerPaging,
            renderPipeline: ConvertPipelineToSqlServer
        );

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
    // Implements [LQL-RENDER-SHARED].
    public static Result<string, SqlError> ToSqlServer(this SelectStatement statement) =>
        StatementRendering.Capture(render: () => SqlServerContext.ToSqlServerSql(statement));

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
