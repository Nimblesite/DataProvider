using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Tests.Shared;

// Implements [CON-SHARED-CORE]. Compile generated source against the actual runtime dependencies.
internal static class GeneratedCodeCompiler
{
    internal static Assembly Compile(string source)
    {
        var runtime = (
            AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string paths
                ? paths.Split(Path.PathSeparator)
                : []
        );
        var references = runtime
            .Concat(
                AppDomain
                    .CurrentDomain.GetAssemblies()
                    .Where(assembly => !assembly.IsDynamic && assembly.Location.Length > 0)
                    .Select(assembly => assembly.Location)
            )
            .Distinct(StringComparer.Ordinal)
            .Select(path => MetadataReference.CreateFromFile(path: path));
        var compilation = CSharpCompilation.Create(
            assemblyName: $"Generated_{Guid.NewGuid():N}",
            syntaxTrees: [CSharpSyntaxTree.ParseText(text: source, path: "Generated.g.cs")],
            references: references,
            options: new CSharpCompilationOptions(
                outputKind: OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );
        using var stream = new MemoryStream();
        var result = compilation.Emit(peStream: stream);
        Xunit.Assert.True(
            result.Success
                && !result.Diagnostics.Any(diagnostic =>
                    diagnostic.Severity == DiagnosticSeverity.Warning
                    && diagnostic.Location.IsInSource
                ),
            string.Join(Environment.NewLine, result.Diagnostics)
        );
        return Assembly.Load(rawAssembly: stream.ToArray());
    }

    internal static async Task<object?> InvokeAsync(
        Assembly assembly,
        string type,
        string method,
        object?[] arguments
    )
    {
        var target = Xunit.Assert.IsAssignableFrom<Type>(assembly.GetType(name: type));
        var member = Xunit.Assert.IsAssignableFrom<MethodInfo>(target.GetMethod(name: method));
        var task = Xunit.Assert.IsAssignableFrom<Task>(
            member.Invoke(obj: null, parameters: arguments)
        );
        await task.ConfigureAwait(false);
        var result = task.GetType().GetProperty(name: "Result")?.GetValue(obj: task);
        Xunit.Assert.NotNull(result);
        var value = result.GetType().GetProperty(name: "Value")?.GetValue(obj: result);
        if (value is Nimblesite.Sql.Model.SqlError error)
            Xunit.Assert.Fail(error.DetailedMessage);
        return value;
    }
}
