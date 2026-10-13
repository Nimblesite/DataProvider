using System.Collections.Immutable;

namespace Nimblesite.Lql.Core.FunctionMapping;

/// <summary>
/// Base implementation for function mapping providers
/// </summary>
public abstract class FunctionMappingProviderBase : IFunctionMappingProvider
{
    private readonly Lazy<ImmutableDictionary<string, FunctionMap>> _functionMappings;
    private readonly Lazy<SqlSyntaxMapping> _syntaxMapping;

    /// <summary>
    /// Initializes a new instance of the FunctionMappingProviderBase class
    /// </summary>
    /// <param name="functionMappingsFactory">Factory for creating function mappings</param>
    /// <param name="syntaxMappingFactory">Factory for creating syntax mapping</param>
    protected FunctionMappingProviderBase(
        Func<ImmutableDictionary<string, FunctionMap>> functionMappingsFactory,
        Func<SqlSyntaxMapping> syntaxMappingFactory
    )
    {
        _functionMappings = new Lazy<ImmutableDictionary<string, FunctionMap>>(
            functionMappingsFactory
        );
        _syntaxMapping = new Lazy<SqlSyntaxMapping>(syntaxMappingFactory);
    }

    /// <summary>
    /// Gets the function mapping for a specific Lql function
    /// </summary>
    /// <param name="lqlFunction">The Lql function name</param>
    /// <returns>The function mapping or null if not found</returns>
    public FunctionMap? GetFunctionMapping(string lqlFunction)
    {
        ArgumentNullException.ThrowIfNull(lqlFunction);

        return _functionMappings.Value.TryGetValue(lqlFunction.ToLowerInvariant(), out var mapping)
            ? mapping
            : null;
    }

    /// <summary>
    /// Gets the syntax mapping for this provider
    /// </summary>
    /// <returns>The syntax mapping</returns>
    public SqlSyntaxMapping GetSyntaxMapping() => _syntaxMapping.Value;

    /// <summary>
    /// Transpiles a function call from Lql to the target dialect
    /// </summary>
    /// <param name="functionName">The function name</param>
    /// <param name="arguments">The function arguments</param>
    /// <returns>The transpiled function call</returns>
    public string TranspileFunction(string functionName, params string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(functionName);

        var mapping = GetFunctionMapping(functionName);
        if (mapping == null)
        {
            // Fallback to default function call format
            return $"{functionName.ToUpperInvariant()}({string.Join(", ", arguments)})";
        }

        if (mapping.RequiresSpecialHandling && mapping.SpecialHandler != null)
        {
            return mapping.SpecialHandler(arguments);
        }

        return $"{mapping.SqlFunction}({string.Join(", ", arguments)})";
    }

    /// <summary>
    /// Formats a LIMIT clause for the target dialect
    /// </summary>
    /// <param name="count">The limit count</param>
    /// <returns>The formatted LIMIT clause</returns>
    public string FormatLimitClause(string count) =>
        FormatClause(format: _syntaxMapping.Value.LimitClause, count: count);

    /// <summary>
    /// Formats an OFFSET clause for the target dialect
    /// </summary>
    /// <param name="count">The offset count</param>
    /// <returns>The formatted OFFSET clause</returns>
    public string FormatOffsetClause(string count) =>
        FormatClause(format: _syntaxMapping.Value.OffsetClause, count: count);

    /// <summary>
    /// Formats an identifier for the target dialect
    /// </summary>
    /// <param name="identifier">The identifier name</param>
    /// <returns>The formatted identifier</returns>
    public string FormatIdentifier(string identifier) =>
        $"{_syntaxMapping.Value.IdentifierQuoteChar}{identifier}{_syntaxMapping.Value.IdentifierQuoteChar}";

    // Implements [LQL-FUNCTION-MAPPING-SHARED-CLAUSE-FORMAT].
    private static string FormatClause(string format, string count) =>
        string.Format(
            provider: global::System.Globalization.CultureInfo.InvariantCulture,
            format: format,
            arg0: count
        );
}
