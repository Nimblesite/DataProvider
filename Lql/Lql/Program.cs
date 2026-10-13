using Nimblesite.Cli.Shared;

namespace Lql;

/// <summary>
/// Unified LQL transpiler CLI tool. Delegates root command construction and
/// dispatch to <see cref="CommandDispatch"/>. Replaces the per-platform
/// tools formerly shipped as <c>Nimblesite.Lql.Cli.Postgres</c> and
/// <c>Nimblesite.Lql.Cli.SQLite</c>.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Entry point.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <returns>Exit code.</returns>
    public static Task<int> Main(string[] args) =>
        CommandDispatch.Run(
            description: "LQL transpiler CLI tool. Transpiles LQL to SQL for a target "
                + "database platform. Pick a platform subcommand: postgres | sqlite.",
            args: args,
            commandFactories: [PostgresCli.BuildCommand, SqliteCli.BuildCommand]
        );
}
