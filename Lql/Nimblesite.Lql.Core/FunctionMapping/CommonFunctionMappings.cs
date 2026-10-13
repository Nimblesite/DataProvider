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

    // Implements [LQL-FUNCTION-MAPPING-SHARED].
    internal static ImmutableDictionary<string, FunctionMap> WithDateFunctions(
        string extract,
        string dateTrunc,
        string currentDate,
        string currentDateExpression,
        string length,
        Func<string[], string>? extractHandler = null,
        Func<string[], string>? dateTruncHandler = null
    ) =>
        WithWindows()
            .SetItems(
                items: ImmutableDictionary<string, FunctionMap>
                    .Empty.Add(
                        key: "extract",
                        value: new FunctionMap(
                            LqlFunction: "extract",
                            SqlFunction: extract,
                            RequiresSpecialHandling: extractHandler is not null,
                            SpecialHandler: extractHandler
                        )
                    )
                    .Add(
                        key: "date_trunc",
                        value: new FunctionMap(
                            LqlFunction: "date_trunc",
                            SqlFunction: dateTrunc,
                            RequiresSpecialHandling: dateTruncHandler is not null,
                            SpecialHandler: dateTruncHandler
                        )
                    )
                    .Add(
                        key: "current_date",
                        value: new FunctionMap(
                            LqlFunction: "current_date",
                            SqlFunction: currentDate,
                            RequiresSpecialHandling: true,
                            SpecialHandler: _ => currentDateExpression
                        )
                    )
                    .Add(
                        key: "length",
                        value: new FunctionMap(LqlFunction: "length", SqlFunction: length)
                    )
            );

    // Implements [LQL-FUNCTION-MAPPING-SHARED].
    internal static SqlSyntaxMapping LimitSyntax(
        string currentDate,
        string dateAdd,
        bool supportsBoolean
    ) =>
        new(
            LimitClause: "LIMIT {0}",
            OffsetClause: "OFFSET {0}",
            DateCurrentFunction: currentDate,
            DateAddFunction: dateAdd,
            StringLengthFunction: "LENGTH",
            StringConcatOperator: "||",
            IdentifierQuoteChar: "\"",
            SupportsBoolean: supportsBoolean
        );
}
