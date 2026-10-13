using Microsoft.CodeAnalysis;

namespace Nimblesite.DataProvider.SqlServer;

// Implements [CON-SHARED-CORE]. The single [Generator] entry point for the
// Nimblesite.DataProvider.SqlServer package. Delegates the incremental
// pipeline to <see cref="SqlFileGenerator"/>; do not register both types.
/// <summary>
/// The registered SQL Server incremental source generator. Scans AdditionalFiles
/// for SQL and configuration and produces strongly-typed data access extension
/// methods via <see cref="SqlFileGenerator"/>.
/// </summary>
[Generator]
public class DataProviderIncrementalSourceGenerator : IIncrementalGenerator
{
    private readonly SqlFileGenerator _inner = new();

    /// <summary>
    /// Initializes the incremental generator pipeline by delegating to the
    /// functional <see cref="SqlFileGenerator"/> implementation.
    /// </summary>
    /// <param name="context">The initialization context provided by Roslyn.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context) =>
        _inner.Initialize(context);
}
