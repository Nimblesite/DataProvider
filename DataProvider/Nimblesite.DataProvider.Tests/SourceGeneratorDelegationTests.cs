using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Nimblesite.DataProvider.SqlServer;

namespace Nimblesite.DataProvider.Tests;

public sealed record SourceGeneratorDelegationTests
{
    // Implements [CON-NOCONFIG]: the CLI owns generation; the legacy generator only informs.
    [Fact]
    public void PassiveGenerator_DoesNotRequireLegacyConfigurationOrEmitSources()
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "CliConsumer",
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        var driver = CSharpGeneratorDriver
            .Create(generators: [new DataProviderIncrementalSourceGenerator().AsSourceGenerator()])
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        Assert.DoesNotContain(
            diagnostics,
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
        );
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "DataProvider005");
        Assert.Empty(driver.GetRunResult().GeneratedTrees);
        Assert.Empty(output.GetDiagnostics());
    }
}
