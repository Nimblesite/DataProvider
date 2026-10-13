using SqlParser;
using SqlParser.Ast;
using SqlParser.Dialects;

namespace Nimblesite.DataProvider.Migration.Core;

// Implements [RLS-DIFF]: PostgreSQL and SQLite both compare live policy
// predicates by parsing a single SELECT and inspecting its projection.
internal static class SqlSelectionExtractor
{
    /// <summary>
    /// Parses <paramref name="sql" /> and returns the projection expression of its
    /// single SELECT statement, or <c>null</c> when the SQL is not exactly one
    /// SELECT statement or cannot be parsed. Callers keep unparseable SQL distinct
    /// unless its text is exactly equal.
    /// </summary>
    internal static Expression? ParseSingleSelectSelection(string sql, Dialect dialect)
    {
        try
        {
            var statements = new Parser().ParseSql(sql, dialect);
            return
                statements.Count == 1
                && statements[0] is Statement.Select select
                && select.Query.Body is SetExpression.SelectExpression body
                ? body.Select.Selection
                : null;
        }
        catch (ParserException)
        {
            return null;
        }
    }
}
