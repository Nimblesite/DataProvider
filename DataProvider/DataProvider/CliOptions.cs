using System.CommandLine;

// Implements [DP-CLI-SHARED-OPTIONS].
namespace DataProvider;

/// <summary>
/// Shared construction of the required CLI options that every codegen
/// subcommand accepts.
/// </summary>
internal static class CliOptions
{
    internal static Option<T> Required<T>(string name, string description) =>
        new Option<T>(name, description: description) { IsRequired = true };

    /// <summary>
    /// Builds the three required options every platform subcommand accepts.
    /// </summary>
    internal static (
        Option<DirectoryInfo> ProjectDir,
        Option<FileInfo> Config,
        Option<DirectoryInfo> OutDir
    ) CommonRequiredOptions(string projectDirDescription) =>
        (
            Required<DirectoryInfo>(name: "--project-dir", description: projectDirDescription),
            Required<FileInfo>(name: "--config", description: "Path to DataProvider.json"),
            Required<DirectoryInfo>(
                name: "--out",
                description: "Output directory for generated .g.cs files"
            )
        );

    /// <summary>
    /// Builds a platform subcommand that accepts the three common required
    /// options plus one platform-specific option, then exits with the run result.
    /// </summary>
    internal static Command BuildPlatformCommand<TExtra>(
        string name,
        string description,
        string projectDirDescription,
        Option<TExtra> extraOption,
        Func<DirectoryInfo, FileInfo, DirectoryInfo, TExtra, Task<int>> runAsync
    )
    {
        var (projectDir, config, outDir) = CommonRequiredOptions(projectDirDescription);
        var command = new Command(name, description) { projectDir, config, outDir, extraOption };
        command.SetHandler(
            async (DirectoryInfo proj, FileInfo cfg, DirectoryInfo output, TExtra extra) =>
            {
                var exit = await runAsync(proj, cfg, output, extra).ConfigureAwait(false);
                Environment.Exit(exit);
            },
            projectDir,
            config,
            outDir,
            extraOption
        );
        return command;
    }
}
