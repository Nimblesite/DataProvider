using System.CommandLine;

namespace DataProvider;

/// <summary>
/// Unified DataProvider codegen tool. <see cref="Main"/> builds a
/// <see cref="RootCommand"/> containing one subcommand per supported
/// platform (<c>postgres</c>, <c>sqlite</c>) and dispatches. Every platform
/// subcommand is authored in its own file (<see cref="PostgresCli"/>,
/// <see cref="SqliteCli"/>) to keep this entry point under 50 lines per the
/// plan.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Entry point. Wires every platform's subcommand onto a single
    /// <see cref="RootCommand"/> and dispatches.
    /// </summary>
    public static Task<int> Main(string[] args)
    {
        var root = new RootCommand(
            "DataProvider codegen tool. Generates type-safe data access code "
                + "from a live database. Pick a platform subcommand: postgres | sqlite | sqlserver."
        )
        {
            PostgresCli.BuildCommand(),
            SqliteCli.BuildCommand(),
            SqlServerCli.BuildCommand(),
        };

        return root.InvokeAsync(args);
    }
}
