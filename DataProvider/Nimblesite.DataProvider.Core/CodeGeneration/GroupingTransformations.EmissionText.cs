// Implements [DP-CLI-SHARED-EMISSION-TEXT].
// Immutable generated-source text only; rendering remains analysed.
namespace Nimblesite.DataProvider.Core.CodeGeneration;

internal static partial class GroupingTransformations
{
    private static readonly global::System.Collections.Immutable.ImmutableArray<string> EmissionBlockLines15 =
    [
        "    {",
        "        // Group raw results by parent key columns",
        "        var parentGroups = rawResults",
        "            .GroupBy(r => new { ",
    ];
    private static readonly global::System.Collections.Immutable.ImmutableArray<string> EmissionBlockLines16 =
    [
        "            })",
        "            .ToList();",
        "",
    ];
    private static readonly global::System.Collections.Immutable.ImmutableArray<string> EmissionBlockLines17 =
    [
        "        foreach (var group in parentGroups)",
        "        {",
        "            var firstItem = group.First();",
    ];
    private static readonly global::System.Collections.Immutable.ImmutableArray<string> EmissionBlockLines18 =
    [
        "                )).ToList()",
        "            );",
        "            result.Add(parent);",
        "        }",
        "        return result.ToImmutableList();",
        "    }",
    ];
    private static readonly global::System.Collections.Immutable.ImmutableArray<string> EmissionBlockLines19 =
    [
        "",
        "        try",
        "        {",
    ];
    private static readonly global::System.Collections.Immutable.ImmutableArray<string> EmissionBlockLines20 =
    [
        "",
        "                using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))",
        "                {",
        "                    while (await reader.ReadAsync().ConfigureAwait(false))",
        "                    {",
    ];
    private static readonly global::System.Collections.Immutable.ImmutableArray<string> EmissionBlockLines21 =
    [
        "                        );",
        "                        rawResults.Add(item);",
        "                    }",
        "                }",
        "            }",
        "",
        "            // Group the raw results into parent-child structure",
        "            var grouped = GroupResults(rawResults.ToImmutable());",
    ];
    private static readonly global::System.Collections.Immutable.ImmutableArray<string> EmissionBlockLines22 =
    [
        "        }",
        "        catch (Exception ex)",
        "        {",
    ];
    private static readonly global::System.Collections.Immutable.ImmutableArray<string> EmissionBlockLines23 =
    [
        "        }",
        "    }",
        "",
    ];
}
