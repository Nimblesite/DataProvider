namespace Nimblesite.DataProvider.Migration.Postgres;

// Implements [MIG-TRIGGER-PG] read-back from
// docs/specs/declarative-triggers-spec.md (GitHub issue 82).

/// <summary>
/// Reads migration-managed triggers back from
/// <c>information_schema.triggers</c> (one row per event) grouped by trigger
/// name so the diff can match declarative trigger guards by name. Only
/// <c>usr_</c>-prefixed triggers are read: triggers created outside the
/// migration tool (e.g. Sync change tracking) stay invisible to the diff so
/// destructive runs never drop them.
/// </summary>
internal static class PostgresTriggerSchemaInspector
{
    public static IReadOnlyList<TriggerDefinition> Inspect(
        NpgsqlConnection connection,
        string schemaName,
        string tableName
    )
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT trigger_name, event_manipulation, action_timing, action_orientation
            FROM information_schema.triggers
            WHERE event_object_schema = @schema AND event_object_table = @table
              AND trigger_name LIKE @managedPattern ESCAPE '\'
            ORDER BY trigger_name
            """;
        command.Parameters.AddWithValue("@schema", schemaName);
        command.Parameters.AddWithValue("@table", tableName);
        command.Parameters.AddWithValue(
            "@managedPattern",
            $"{TriggerDdlSupport.ManagedTriggerPrefix.Replace("_", "\\_", StringComparison.Ordinal)}%"
        );

        var rows = new List<PostgresTriggerRow>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                rows.Add(
                    new PostgresTriggerRow(
                        Name: reader.GetString(0),
                        Event: reader.GetString(1),
                        Timing: reader.GetString(2),
                        Orientation: reader.GetString(3)
                    )
                );
            }
        }

        return rows.GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select(ToTrigger)
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static TriggerDefinition ToTrigger(IGrouping<string, PostgresTriggerRow> group) =>
        new()
        {
            // Strip the managed prefix: the model holds the declared name.
            Name = group.Key[TriggerDdlSupport.ManagedTriggerPrefix.Length..],
            Timing = group.Any(r => r.Timing.Equals("AFTER", StringComparison.OrdinalIgnoreCase))
                ? TriggerTiming.After
                : TriggerTiming.Before,
            ForEachRow = group.All(r =>
                r.Orientation.Equals("ROW", StringComparison.OrdinalIgnoreCase)
            ),
            Events = group.Select(r => ToEvent(r.Event)).Distinct().ToList(),
        };

    private static TriggerEvent ToEvent(string eventManipulation) =>
        eventManipulation.ToUpperInvariant() switch
        {
            "INSERT" => TriggerEvent.Insert,
            "UPDATE" => TriggerEvent.Update,
            _ => TriggerEvent.Delete,
        };
}

/// <summary>One information_schema.triggers row.</summary>
internal sealed record PostgresTriggerRow(
    string Name,
    string Event,
    string Timing,
    string Orientation
);
