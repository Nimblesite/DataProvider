using Nimblesite.Sql.Model;
using SqlResult = Outcome.Result<string, Nimblesite.Sql.Model.SqlError>;
using SqlResultError = Outcome.Result<string, Nimblesite.Sql.Model.SqlError>.Error<
    string,
    Nimblesite.Sql.Model.SqlError
>;
using SqlResultOk = Outcome.Result<string, Nimblesite.Sql.Model.SqlError>.Ok<
    string,
    Nimblesite.Sql.Model.SqlError
>;

namespace Nimblesite.Lql.Core;

// Implements [LQL-RENDER-SHARED], [LQL-CTE] and [LQL-DERIVED-TABLE].
internal static class StatementRendering
{
    internal static SqlResult Render(
        LqlStatement statement,
        Func<string?, string?, SqlPaging> paging,
        Func<Pipeline, string> renderPipeline,
        Func<string, string>? formatIdentifier = null
    )
    {
        ArgumentNullException.ThrowIfNull(statement);
        return LqlStatementValidator.Validate(statement) is SqlError error
            ? new SqlResultError(error)
            : Capture(render: () =>
                RenderNode(statement, paging, renderPipeline, formatIdentifier)
            );
    }

    internal static SqlResult Capture(Func<string> render)
    {
        try
        {
            return new SqlResultOk(render());
        }
        catch (Exception exception)
        {
            return new SqlResultError(SqlError.FromException(exception));
        }
    }

    private static string RenderNode(
        LqlStatement statement,
        Func<string?, string?, SqlPaging> paging,
        Func<Pipeline, string> renderPipeline,
        Func<string, string>? formatIdentifier
    ) =>
        statement.AstNode switch
        {
            { } node when SubqueryLayout.Applies(node) => SubqueryLayout.Render(node, paging),
            Pipeline pipeline => renderPipeline(pipeline),
            Identifier identifier =>
                $"SELECT *\nFROM {formatIdentifier?.Invoke(identifier.Name) ?? identifier.Name}",
            _ => "-- Unknown AST node type",
        };
}
