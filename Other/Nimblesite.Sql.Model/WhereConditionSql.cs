namespace Nimblesite.Sql.Model;

/// <summary>
/// Renders a WHERE condition token sequence for every SQL dialect.
/// Implements [DP-SQL-MODEL-DIALECTS].
/// </summary>
public static class WhereConditionSql
{
    /// <summary>
    /// Joins formatted WHERE tokens with single spaces. Two adjacent predicates
    /// with no explicit logical operator between them are joined with AND.
    /// </summary>
    /// <param name="conditions">The WHERE condition tokens in order</param>
    /// <param name="format">Dialect-specific formatter for a single token</param>
    /// <returns>The WHERE clause body without the WHERE keyword</returns>
    public static string Join(
        IReadOnlyList<WhereCondition> conditions,
        Func<WhereCondition, string> format
    ) =>
        string.Concat(
            conditions.Select(
                (condition, i) =>
                    (i == 0 ? "" : Separator(conditions[i - 1], condition)) + format(condition)
            )
        );

    private static string Separator(WhereCondition previous, WhereCondition current) =>
        EndsPredicate(previous) && StartsPredicate(current) ? " AND " : " ";

    private static bool EndsPredicate(WhereCondition condition) =>
        condition is ComparisonCondition or ExpressionCondition or Parenthesis { IsOpening: false };

    private static bool StartsPredicate(WhereCondition condition) =>
        condition is ComparisonCondition or ExpressionCondition or Parenthesis { IsOpening: true };
}
