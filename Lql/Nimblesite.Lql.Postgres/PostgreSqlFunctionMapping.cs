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
        CommonFunctionMappings
            .WithWindows()
            .SetItems(
                items: new Dictionary<string, FunctionMap>
                {
                    ["extract"] = new("extract", "EXTRACT"),
                    ["date_trunc"] = new("date_trunc", "DATE_TRUNC"),
                    ["current_date"] = new(
                        "current_date",
                        "CURRENT_DATE",
                        RequiresSpecialHandling: true,
                        SpecialHandler: _ => "CURRENT_DATE"
                    ),
                    ["length"] = new("length", "LENGTH"),
                }
            );

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
