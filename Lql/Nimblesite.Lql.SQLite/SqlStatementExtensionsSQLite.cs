using Nimblesite.Sql.Model;
using Outcome;

namespace Nimblesite.Lql.SQLite;

/// <summary>
/// SQLite-specific extension methods for SelectStatement
/// </summary>
public static class SqlStatementExtensionsSQLite
{
    /// <summary>
    /// Converts a LqlStatement to SQLite syntax
    /// TODO: this should not return a result because it can't fail
    /// </summary>
    /// <param name="statement">The LqlStatement to convert</param>
    /// <returns>A Result containing either SQLite SQL string or a SqlError</returns>
    // Implements [LQL-RENDER-SHARED].
    public static Result<string, SqlError> ToSQLite(this LqlStatement statement) =>
        StatementRendering.Render(
            statement: statement,
            paging: SqlPaging.LimitOffset,
            renderPipeline: ConvertPipelineToSQLite
        );

    /// <summary>
    /// Converts a Nimblesite.Sql.Model.SelectStatement to SQLite syntax
    /// TODO: this should not return a result because it can't fail
    /// </summary>
    /// <param name="statement">The SelectStatement to convert</param>
    /// <returns>A Result containing either SQLite SQL string or a SqlError</returns>
    // Implements [LQL-RENDER-SHARED].
    public static Result<string, SqlError> ToSQLite(this SelectStatement statement) =>
        StatementRendering.Capture(render: () => SQLiteContext.ToSQLiteSql(statement));

    private static string ConvertPipelineToSQLite(Pipeline pipeline)
    {
        var context = new SQLiteContext();
        return PipelineProcessor.ConvertPipelineToSql(pipeline, context);
    }
}
