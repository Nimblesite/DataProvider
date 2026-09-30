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
    public static Result<string, SqlError> ToSQLite(this LqlStatement statement)
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
                var sql = ConvertPipelineToSQLite(pipeline);
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
    /// Converts a Nimblesite.Sql.Model.SelectStatement to SQLite syntax
    /// TODO: this should not return a result because it can't fail
    /// </summary>
    /// <param name="statement">The SelectStatement to convert</param>
    /// <returns>A Result containing either SQLite SQL string or a SqlError</returns>
    public static Result<string, SqlError> ToSQLite(this SelectStatement statement)
    {
        try
        {
            var sql = SQLiteContext.ToSQLiteSql(statement);
            return new Result<string, SqlError>.Ok<string, SqlError>(sql);
        }
        catch (Exception ex)
        {
            return new Result<string, SqlError>.Error<string, SqlError>(SqlError.FromException(ex));
        }
    }

    private static string ConvertPipelineToSQLite(Pipeline pipeline)
    {
        var context = new SQLiteContext();
        return PipelineProcessor.ConvertPipelineToSql(pipeline, context);
    }
}
