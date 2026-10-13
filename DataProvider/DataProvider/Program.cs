using Nimblesite.Cli.Shared;

namespace DataProvider;

/// <summary>
/// Unified DataProvider codegen tool. Delegates root command construction and
/// dispatch to <see cref="CommandDispatch"/>. Every platform subcommand is
/// authored in its own file (<see cref="PostgresCli"/>, <see cref="SqliteCli"/>,
/// <see cref="SqlServerCli"/>).
/// </summary>
internal static class Program
{
    /// <summary>
    /// Entry point. Wires every platform's subcommand onto a single
    /// root command and dispatches.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <returns>Exit code.</returns>
    public static Task<int> Main(string[] args) =>
        CommandDispatch.Run(
            description: "DataProvider codegen tool. Generates type-safe data access code "
                + "from a live database. Pick a platform subcommand: postgres | sqlite | sqlserver.",
            args: args,
            commandFactories:
            [
                PostgresCli.BuildCommand,
                SqliteCli.BuildCommand,
                SqlServerCli.BuildCommand,
            ]
        );
}
