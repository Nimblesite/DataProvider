using Microsoft.CodeAnalysis;

namespace Nimblesite.DataProvider.SqlServer;

/// <summary>
/// Compatibility generator that directs consumers to CLI-based code generation.
/// It does not read legacy configuration or emit sources. Implements [CON-NOCONFIG].
/// </summary>
[Generator]
public class DataProviderIncrementalSourceGenerator : IIncrementalGenerator
{
    /// <summary>Registers the informational CLI delegation diagnostic.</summary>
    /// <param name="context">The initialization context provided by Roslyn.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context) =>
        context.RegisterSourceOutput(
            context.CompilationProvider,
            static (production, _) =>
                production.ReportDiagnostic(
                    Diagnostic.Create(
                        descriptor: new DiagnosticDescriptor(
                            id: "DataProvider005",
                            title: "Source generator delegated to CLI",
                            messageFormat: "Code generation is handled by CLI in MSBuild target, not by this incremental generator",
                            category: "DataProvider",
                            defaultSeverity: DiagnosticSeverity.Info,
                            isEnabledByDefault: true
                        ),
                        location: Location.None
                    )
                )
        );
}
