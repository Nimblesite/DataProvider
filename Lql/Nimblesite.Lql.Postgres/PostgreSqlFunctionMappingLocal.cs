using Nimblesite.Lql.Core.FunctionMapping;

namespace Nimblesite.Lql.Postgres;

/// <summary>
/// Local PostgreSQL-specific function mapping implementation for the Lql project
/// </summary>
public sealed class PostgreSqlFunctionMappingLocal : FunctionMappingProviderBase
{
    /// <summary>
    /// Singleton instance of the PostgreSQL function mapping
    /// </summary>
    public static readonly PostgreSqlFunctionMappingLocal Instance = new();

    /// <summary>
    /// Private constructor to enforce singleton pattern
    /// </summary>
    private PostgreSqlFunctionMappingLocal()
        : base(
            PostgreSqlFunctionMapping.CreateFunctionMappings,
            PostgreSqlFunctionMapping.CreateSyntaxMapping
        ) { }
}
