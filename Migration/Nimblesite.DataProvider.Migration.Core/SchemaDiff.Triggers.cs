namespace Nimblesite.DataProvider.Migration.Core;

// Implements [MIG-TRIGGER-DIFF] from docs/specs/declarative-triggers-spec.md
// (GitHub issue 82). Mirrors the RLS policy diff: triggers are matched by
// name per table; drops are destructive and gated behind allowDestructive.

public static partial class SchemaDiff
{
    private static IEnumerable<SchemaOperation> CalculateTriggerDiff(
        TableDefinition? current,
        TableDefinition desired,
        bool allowDestructive,
        ILogger? logger
    )
    {
        var currentNames =
            current?.Triggers.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var trigger in desired.Triggers)
        {
            if (!currentNames.Contains(trigger.Name))
            {
                logger?.LogDebug(
                    "Creating trigger {Trigger} on {Schema}.{Table}",
                    trigger.Name,
                    desired.Schema,
                    desired.Name
                );
                yield return new CreateTriggerOperation(desired.Schema, desired.Name, trigger);
            }
        }

        if (!allowDestructive || current is null)
        {
            yield break;
        }

        var desiredNames = desired
            .Triggers.Select(t => t.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var trigger in current.Triggers)
        {
            if (!desiredNames.Contains(trigger.Name))
            {
                logger?.LogWarning(
                    "Trigger {Trigger} on {Schema}.{Table} will be DROPPED",
                    trigger.Name,
                    desired.Schema,
                    desired.Name
                );
                yield return new DropTriggerOperation(desired.Schema, desired.Name, trigger.Name);
            }
        }
    }
}
