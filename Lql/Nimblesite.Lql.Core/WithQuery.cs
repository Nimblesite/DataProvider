using System.Collections.Immutable;

namespace Nimblesite.Lql.Core;

/// <summary>
/// A statement with common table expressions: <c>with name as (pipeline) main-pipeline</c>.
/// Implements [LQL-CTE].
/// </summary>
/// <param name="Ctes">The named pipelines, in declaration order.</param>
/// <param name="Body">The main pipeline, which may reference the CTE names as tables.</param>
public sealed record WithQuery(ImmutableArray<CommonTableExpression> Ctes, Pipeline Body) : INode;

/// <summary>
/// One named pipeline of a <see cref="WithQuery"/>.
/// </summary>
/// <param name="Name">The CTE name.</param>
/// <param name="Query">The pipeline the name stands for.</param>
public sealed record CommonTableExpression(string Name, Pipeline Query);
