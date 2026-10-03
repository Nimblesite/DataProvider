using System.Collections.Immutable;

namespace Nimblesite.Lql.Core;

/// <summary>
/// A dialect's rendering of LIMIT/OFFSET: a prefix placed after <c>SELECT</c> (such as
/// SQL Server's <c>TOP n</c>) and trailing clause lines. Implements [LQL-SUBQUERY-LAYOUT].
/// </summary>
/// <param name="SelectPrefix">Text inserted after <c>SELECT</c>, or empty.</param>
/// <param name="TailLines">Clause lines appended after ORDER BY.</param>
public sealed record SqlPaging(string SelectPrefix, ImmutableArray<string> TailLines)
{
    /// <summary>
    /// LIMIT/OFFSET lines used by SQLite and PostgreSQL.
    /// </summary>
    /// <param name="limit">The row limit, if any.</param>
    /// <param name="offset">The row offset, if any.</param>
    /// <returns>The paging rendering.</returns>
    public static SqlPaging LimitOffset(string? limit, string? offset) =>
        new(
            "",
            [
                .. limit is null ? Array.Empty<string>() : [$"LIMIT {limit}"],
                .. offset is null ? Array.Empty<string>() : [$"OFFSET {offset}"],
            ]
        );
}
