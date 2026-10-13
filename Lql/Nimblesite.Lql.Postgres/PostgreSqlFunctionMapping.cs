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
        CommonFunctionMappings.WithDateFunctions(
            extract: "EXTRACT",
            dateTrunc: "DATE_TRUNC",
            currentDate: "CURRENT_DATE",
            currentDateExpression: "CURRENT_DATE",
            length: "LENGTH"
        );

    /// <summary>
    /// Creates the PostgreSQL syntax mapping
    /// </summary>
    /// <returns>The PostgreSQL syntax mapping</returns>
    internal static SqlSyntaxMapping CreateSyntaxMapping() =>
        CommonFunctionMappings.LimitSyntax(
            currentDate: "CURRENT_DATE",
            dateAdd: "INTERVAL '{0} {1}'",
            supportsBoolean: true
        );
}
