using System.Collections;
using System.Diagnostics;
using System.Text.Json;

namespace Nimblesite.DataProvider.Tests;

public sealed partial record QueryBuilderPlatformE2ETests
{
    // Implements [CON-SHARED-CORE]. Exercise the shipped CLI, then execute its compiled output.
    private static async Task AssertCliAsync(DbConnection connection, string provider)
    {
        var directory = Directory.CreateTempSubdirectory(prefix: "codegen_cli_");
        try
        {
            var output = Path.Combine(directory.FullName, "generated");
            var config = Path.Combine(directory.FullName, "DataProvider.json");
            await File.WriteAllTextAsync(
                    path: config,
                    contents: JsonSerializer.Serialize(
                        new { ConnectionString = connection.ConnectionString }
                    )
                )
                .ConfigureAwait(true);
            await File.WriteAllTextAsync(
                    path: Path.Combine(directory.FullName, "GetCliProducts.sql"),
                    contents: "SELECT name AS name FROM products ORDER BY name"
                )
                .ConfigureAwait(true);
            await RunCliAsync(
                    provider: provider,
                    project: directory.FullName,
                    config: config,
                    output: output
                )
                .ConfigureAwait(true);
            var source = await File.ReadAllTextAsync(
                    path: Path.Combine(output, "GetCliProducts.g.cs")
                )
                .ConfigureAwait(true);
            var assembly = GeneratedCodeCompiler.Compile(source: source);
            var result = await GeneratedCodeCompiler
                .InvokeAsync(
                    assembly: assembly,
                    type: "Generated.GetCliProductsExtensions",
                    method: "GetCliProductsAsync",
                    arguments: [connection]
                )
                .ConfigureAwait(true);
            Assert.Equal(8, Assert.IsAssignableFrom<IEnumerable>(result).Cast<object>().Count());
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static async Task RunCliAsync(
        string provider,
        string project,
        string config,
        string output
    )
    {
        var buildConfiguration =
            new DirectoryInfo(path: AppContext.BaseDirectory).Parent?.Name ?? "Debug";
        var cli = Path.GetFullPath(
            path: $"../../../../DataProvider/bin/{buildConfiguration}/net10.0/DataProvider.dll",
            basePath: AppContext.BaseDirectory
        );
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(fileName: "dotnet")
            {
                ArgumentList =
                {
                    cli,
                    provider,
                    "--project-dir",
                    project,
                    "--config",
                    config,
                    "--out",
                    output,
                },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
        Assert.True(process.Start());
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(delay: TimeSpan.FromSeconds(30));
        await process.WaitForExitAsync(cancellationToken: deadline.Token).ConfigureAwait(true);
        Assert.True(
            process.ExitCode == 0,
            $"CLI failed: {await stdout.ConfigureAwait(true)} {await stderr.ConfigureAwait(true)}"
        );
    }
}
