using System.Collections.Immutable;

namespace Nimblesite.Lql.Core.FunctionMapping;

// Implements [LQL-FUNCTION-MAPPING-SHARED].
internal static class CommonFunctionMappings
{
    internal static ImmutableDictionary<string, FunctionMap> Standard() =>
        Simple(names: ["sum", "avg", "min", "max", "coalesce", "upper", "lower"])
            .Add(
                key: "count",
                value: new FunctionMap(
                    LqlFunction: "count",
                    SqlFunction: "COUNT",
                    RequiresSpecialHandling: true,
                    SpecialHandler: Count
                )
            );

    internal static ImmutableDictionary<string, FunctionMap> WithWindows() =>
        Standard()
            .AddRange(
                pairs: Simple(
                    names: ["substring", "row_number", "rank", "dense_rank", "lag", "lead"]
                )
            )
            .Add(
                key: "exists",
                value: new FunctionMap(
                    LqlFunction: "exists",
                    SqlFunction: "EXISTS",
                    RequiresSpecialHandling: true,
                    SpecialHandler: args => $"EXISTS ({string.Join(" ", args)})"
                )
            );

    private static ImmutableDictionary<string, FunctionMap> Simple(IEnumerable<string> names) =>
        names.ToImmutableDictionary(
            keySelector: name => name,
            elementSelector: name => new FunctionMap(
                LqlFunction: name,
                SqlFunction: name.ToUpperInvariant()
            )
        );

    private static string Count(string[] args) =>
        args.Length == 1 && args[0] == "*" ? "COUNT(*)" : $"COUNT({string.Join(", ", args)})";
}
