using System.Collections.Immutable;
using Nimblesite.Lql.Core.FunctionMapping;

namespace Nimblesite.Lql.Postgres;

/// <summary>
/// PostgreSQL-specific function mapping implementation
/// </summary>
public sealed class PostgreSqlFunctionMapping : FunctionMappingProviderBase
{
    /// <summary>
    /// Singleton instance of the PostgreSQL function mapping
    /// </summary>
    public static readonly PostgreSqlFunctionMapping Instance = new();

    /// <summary>
    /// Private constructor to enforce singleton pattern
    /// </summary>
    private PostgreSqlFunctionMapping()
        : base(CreateFunctionMappings, CreateSyntaxMapping) { }

    /// <summary>
    /// Creates the PostgreSQL function mappings
    /// </summary>
    /// <returns>Dictionary of function mappings</returns>
    internal static ImmutableDictionary<string, FunctionMap> CreateFunctionMappings() =>
        new Dictionary<string, FunctionMap>
        {
            ["count"] = new(
                "count",
                "COUNT",
                RequiresSpecialHandling: true,
                SpecialHandler: args =>
                    args.Length == 1 && args[0] == "*"
                        ? "COUNT(*)"
                        : $"COUNT({string.Join(", ", args)})"
            ),
            ["sum"] = new("sum", "SUM"),
            ["avg"] = new("avg", "AVG"),
            ["min"] = new("min", "MIN"),
            ["max"] = new("max", "MAX"),
            ["coalesce"] = new("coalesce", "COALESCE"),
            ["extract"] = new("extract", "EXTRACT"),
            ["date_trunc"] = new("date_trunc", "DATE_TRUNC"),
            ["current_date"] = new(
                "current_date",
                "CURRENT_DATE",
                RequiresSpecialHandling: true,
                SpecialHandler: _ => "CURRENT_DATE"
            ),
            ["length"] = new("length", "LENGTH"),
            ["upper"] = new("upper", "UPPER"),
            ["lower"] = new("lower", "LOWER"),
            ["substring"] = new("substring", "SUBSTRING"),
            ["exists"] = new(
                "exists",
                "EXISTS",
                RequiresSpecialHandling: true,
                SpecialHandler: args => $"EXISTS ({string.Join(" ", args)})"
            ),
            ["row_number"] = new("row_number", "ROW_NUMBER"),
            ["rank"] = new("rank", "RANK"),
            ["dense_rank"] = new("dense_rank", "DENSE_RANK"),
            ["lag"] = new("lag", "LAG"),
            ["lead"] = new("lead", "LEAD"),
        }.ToImmutableDictionary();

    /// <summary>
    /// Creates the PostgreSQL syntax mapping
    /// </summary>
    /// <returns>The PostgreSQL syntax mapping</returns>
    internal static SqlSyntaxMapping CreateSyntaxMapping() =>
        new(
            LimitClause: "LIMIT {0}",
            OffsetClause: "OFFSET {0}",
            DateCurrentFunction: "CURRENT_DATE",
            DateAddFunction: "INTERVAL '{0} {1}'",
            StringLengthFunction: "LENGTH",
            StringConcatOperator: "||",
            IdentifierQuoteChar: "\"",
            SupportsBoolean: true
        );
}
