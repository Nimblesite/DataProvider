using Nimblesite.Sql.Model;

namespace Nimblesite.Lql.Core;

/// <summary>
/// Represents a JOIN operation (INNER, LEFT, CROSS, etc.).
/// </summary>
public sealed class JoinStep : StepBase
{
    /// <summary>
    /// Gets the join relationship containing table, condition, and join type.
    /// </summary>
    public required JoinRelationship JoinRelationship { get; init; }

    /// <summary>
    /// Gets the parenthesized pipeline joined as a derived table, when the join source is
    /// <c>(pipeline)</c> rather than a table name. Implements [LQL-DERIVED-TABLE].
    /// </summary>
    public Pipeline? DerivedTable { get; init; }
}
