using System.Collections.Immutable;
using Nimblesite.Sql.Model;

namespace Nimblesite.Lql.Core;

/// <summary>
/// The clauses of one pipeline, collected from its steps for layout rendering.
/// Implements [LQL-SUBQUERY-LAYOUT].
/// </summary>
internal sealed record PipelineShape(
    string BaseTable,
    ImmutableArray<JoinStep> Joins,
    // One condition per filter step; successive filters are ANDed when rendered.
    ImmutableArray<WhereCondition> Where,
    ImmutableArray<ColumnInfo> Columns,
    bool Distinct,
    ImmutableArray<string> GroupBy,
    string? Having,
    ImmutableArray<(string Column, string Direction)> OrderBy,
    string? Limit,
    string? Offset
)
{
    internal static PipelineShape From(Pipeline pipeline) =>
        pipeline.Steps.Aggregate(
            new PipelineShape("", [], [], [], false, [], null, [], null, null),
            (shape, step) => shape.With(step)
        );

    private PipelineShape With(IStep step) =>
        step switch
        {
            IdentityStep { Base: Identifier table } => this with { BaseTable = table.Name },
            JoinStep join => this with { Joins = Joins.Add(join) },
            FilterStep filter => this with { Where = Where.Add(filter.Condition) },
            SelectStep select => this with { Columns = select.Columns, Distinct = false },
            SelectDistinctStep select => this with { Columns = select.Columns, Distinct = true },
            GroupByStep groupBy => this with { GroupBy = groupBy.Columns },
            HavingStep having => this with { Having = having.Condition },
            OrderByStep orderBy => this with { OrderBy = orderBy.OrderItems },
            LimitStep limit => this with { Limit = limit.Count },
            OffsetStep offset => this with { Offset = offset.Count },
            _ => throw new NotSupportedException(
                $"{step.GetType().Name} is not supported in a subquery or CTE pipeline"
            ),
        };
}
