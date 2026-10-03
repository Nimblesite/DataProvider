using System.Collections.Immutable;
using Nimblesite.Sql.Model;

namespace Nimblesite.Lql.Core;

/// <summary>
/// Renders nested pipelines — common table expressions, derived-table joins, and
/// EXISTS/IN subquery bodies — in one clause-per-line layout shared by every dialect.
/// Only paging syntax differs per dialect. Implements [LQL-CTE], [LQL-DERIVED-TABLE]
/// and [LQL-SUBQUERY-LAYOUT].
/// </summary>
public static class SubqueryLayout
{
    private const string Indent = "    ";

    /// <summary>
    /// True when the statement needs this layout: it declares CTEs or joins a derived table.
    /// </summary>
    /// <param name="node">The statement's AST node.</param>
    /// <returns>Whether <see cref="Render"/> should render the statement.</returns>
    public static bool Applies(INode? node) =>
        node is WithQuery
        || node is Pipeline pipeline
            && pipeline.Steps.OfType<JoinStep>().Any(join => join.DerivedTable is not null);

    /// <summary>
    /// Renders a statement that <see cref="Applies"/> to.
    /// </summary>
    /// <param name="node">The statement's AST node.</param>
    /// <param name="paging">The dialect's LIMIT/OFFSET rendering.</param>
    /// <returns>The SQL text.</returns>
    public static string Render(INode node, Func<string?, string?, SqlPaging> paging) =>
        node switch
        {
            WithQuery query => RenderWith(query, paging),
            Pipeline pipeline => Query(pipeline, paging),
            _ => throw new NotSupportedException($"Cannot lay out {node.GetType().Name}"),
        };

    /// <summary>
    /// Renders an EXISTS/IN subquery body. It may correlate with the outer query, so table
    /// qualifiers are kept, and it selects <c>1</c> when no columns are chosen.
    /// </summary>
    internal static string CorrelatedBody(Pipeline pipeline) =>
        string.Join(
            "\n",
            Lines(PipelineShape.From(pipeline), [], NoPaging, defaultColumns: "1", correlated: true)
        );

    private static string RenderWith(WithQuery query, Func<string?, string?, SqlPaging> paging) =>
        "WITH "
        + string.Join(
            ",\n",
            query.Ctes.Select(cte => $"{cte.Name} AS {Block(Query(cte.Query, paging))}")
        )
        + $"\n{Query(query.Body, paging)}";

    private static string Query(Pipeline pipeline, Func<string?, string?, SqlPaging> paging)
    {
        var shape = PipelineShape.From(pipeline);
        return string.Join(
            "\n",
            Lines(shape, Aliases(shape), paging, defaultColumns: "*", correlated: false)
        );
    }

    private static IEnumerable<string> Lines(
        PipelineShape shape,
        ImmutableArray<string> aliases,
        Func<string?, string?, SqlPaging> paging,
        string defaultColumns,
        bool correlated
    )
    {
        var qualifiers = Qualifiers(shape, aliases, correlated);
        var page = paging(shape.Limit, shape.Offset);
        return
        [
            SelectLine(shape, qualifiers, page.SelectPrefix, defaultColumns),
            Source("FROM", shape.BaseTable, aliases.FirstOrDefault()),
            .. shape.Joins.Select(
                (join, i) => JoinLine(join, aliases.ElementAtOrDefault(i + 1), qualifiers, paging)
            ),
            .. Clause("WHERE", shape.Where.IsEmpty ? null : Where(shape.Where, qualifiers)),
            .. Clause(
                "GROUP BY",
                shape.GroupBy.IsEmpty
                    ? null
                    : string.Join(
                        ", ",
                        shape.GroupBy.Select(c => QualifierRewriter.Rewrite(c, qualifiers))
                    )
            ),
            .. Clause(
                "HAVING",
                shape.Having is null ? null : QualifierRewriter.Rewrite(shape.Having, qualifiers)
            ),
            .. Clause(
                "ORDER BY",
                shape.OrderBy.IsEmpty
                    ? null
                    : string.Join(
                        ", ",
                        shape.OrderBy.Select(o =>
                            $"{QualifierRewriter.Rewrite(o.Column, qualifiers)} {o.Direction}"
                        )
                    )
            ),
            .. page.TailLines,
        ];
    }

    private static string SelectLine(
        PipelineShape shape,
        IReadOnlyDictionary<string, string> qualifiers,
        string prefix,
        string defaultColumns
    ) =>
        $"SELECT {prefix}{(shape.Distinct ? "DISTINCT " : "")}"
        + (
            shape.Columns.IsEmpty
                ? defaultColumns
                : string.Join(", ", shape.Columns.Select(c => Column(c, qualifiers)))
        );

    private static string Source(string keyword, string table, string? alias) =>
        table.Length == 0
            ? throw new NotSupportedException("A subquery or CTE pipeline needs a base table")
        : string.IsNullOrEmpty(alias) ? $"{keyword} {table}"
        : $"{keyword} {table} {alias}";

    private static string JoinLine(
        JoinStep join,
        string? alias,
        IReadOnlyDictionary<string, string> qualifiers,
        Func<string?, string?, SqlPaging> paging
    )
    {
        var relationship = join.JoinRelationship;
        var source = join.DerivedTable is { } derived
            ? Block(Query(derived, paging))
            : relationship.RightTable;
        var on = string.IsNullOrEmpty(relationship.Condition)
            ? ""
            : $" ON {QualifierRewriter.Rewrite(relationship.Condition, qualifiers)}";
        return $"{Source(relationship.JoinType, source, alias)}{on}";
    }

    // Successive filters narrow the rows; each is parenthesized when there are several so
    // an OR inside one filter cannot bind across the AND.
    private static string Where(
        ImmutableArray<WhereCondition> filters,
        IReadOnlyDictionary<string, string> qualifiers
    ) =>
        filters.Length == 1
            ? Condition(filters[0], qualifiers)
            : string.Join(" AND ", filters.Select(f => $"({Condition(f, qualifiers)})"));

    private static string Condition(
        WhereCondition condition,
        IReadOnlyDictionary<string, string> qualifiers
    ) =>
        condition switch
        {
            ExpressionCondition expression => QualifierRewriter.Rewrite(
                expression.Expression,
                qualifiers
            ),
            ComparisonCondition comparison =>
                $"{Column(comparison.Left, qualifiers)} {comparison.Operator.ToSql()} {QualifierRewriter.Rewrite(comparison.Right, qualifiers)}",
            _ => throw new NotSupportedException(
                $"Unsupported filter condition: {condition.GetType().Name}"
            ),
        };

    private static string Column(
        ColumnInfo column,
        IReadOnlyDictionary<string, string> qualifiers
    ) =>
        column switch
        {
            NamedColumn named => Qualified(named.TableAlias, named.Name, qualifiers),
            ExpressionColumn expression => QualifierRewriter.Rewrite(
                expression.Expression,
                qualifiers
            ),
            WildcardColumn wildcard => Qualified(wildcard.TableAlias, "*", qualifiers),
            _ => throw new NotSupportedException($"Unsupported column: {column.GetType().Name}"),
        } + (column.Alias is null ? "" : $" AS {column.Alias}");

    private static string Qualified(
        string? table,
        string name,
        IReadOnlyDictionary<string, string> qualifiers
    ) =>
        table is null ? name
        : qualifiers.TryGetValue(table, out var alias)
            ? (alias.Length == 0 ? name : $"{alias}.{name}")
        : $"{table}.{name}";

    // Joined queries alias every source by its initials (users -> u, high_value_customers
    // -> hvc); a single-table query needs no aliases.
    private static ImmutableArray<string> Aliases(PipelineShape shape) =>
        shape.Joins.IsEmpty
            ? []
            :
            [
                .. new[] { shape.BaseTable }
                    .Concat(shape.Joins.Select(join => join.JoinRelationship.RightTable))
                    .Aggregate(
                        ImmutableList<string>.Empty,
                        (taken, name) => taken.Add(UniqueAlias(Initials(name), taken))
                    ),
            ];

    // Qualifiers become the source aliases. Without aliases a standalone single-table body
    // drops its qualifier; a correlated body keeps it to tell inner from outer columns.
    private static ImmutableDictionary<string, string> Qualifiers(
        PipelineShape shape,
        ImmutableArray<string> aliases,
        bool correlated
    ) =>
        correlated ? ImmutableDictionary<string, string>.Empty
        : aliases.IsEmpty
            ? ImmutableDictionary<string, string>
                .Empty.WithComparers(StringComparer.OrdinalIgnoreCase)
                .Add(shape.BaseTable, "")
        : new[] { shape.BaseTable }
            .Concat(shape.Joins.Select(join => join.JoinRelationship.RightTable))
            .Zip(aliases)
            .GroupBy(pair => pair.First, StringComparer.OrdinalIgnoreCase)
            .ToImmutableDictionary(
                g => g.Key,
                g => g.First().Second,
                StringComparer.OrdinalIgnoreCase
            );

    private static string Initials(string name) =>
        string.Concat(
            name.Split('_', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToLowerInvariant(part[0]))
        );

    private static string UniqueAlias(string alias, ImmutableList<string> taken) =>
        Enumerable
            .Range(1, taken.Count + 1)
            .Select(n => n == 1 ? alias : $"{alias}{n}")
            .First(candidate => !taken.Contains(candidate));

    private static IEnumerable<string> Clause(string keyword, string? body) =>
        body is null ? [] : [$"{keyword} {body}"];

    private static string Block(string sql) =>
        $"(\n{string.Join("\n", sql.Split('\n').Select(line => Indent + line))}\n)";

    private static SqlPaging NoPaging(string? limit, string? offset) =>
        limit is null && offset is null
            ? new SqlPaging("", [])
            : throw new NotSupportedException(
                "limit/offset are not supported inside EXISTS or IN subqueries"
            );
}
