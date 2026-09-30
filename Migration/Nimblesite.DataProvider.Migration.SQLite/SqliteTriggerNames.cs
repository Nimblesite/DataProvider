namespace Nimblesite.DataProvider.Migration.SQLite;

/// <summary>
/// Shared reading/parsing for migration-managed SQLite trigger names of the
/// form <c>{prefix}{event}_{name}_{table}</c>. Used by the RLS and
/// declarative trigger inspectors.
/// </summary>
internal static class SqliteTriggerNames
{
    /// <summary>DML event tokens used in managed trigger names.</summary>
    public static IReadOnlyList<string> DmlEventTokens { get; } = ["insert", "update", "delete"];

    public static List<string> Read(SqliteConnection connection, string tableName, string pattern)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name FROM sqlite_master
            WHERE type = 'trigger' AND tbl_name = @table AND name LIKE @pattern
            ORDER BY name
            """;
        command.Parameters.AddWithValue("@table", tableName);
        command.Parameters.AddWithValue("@pattern", pattern);
        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }
        return names;
    }

    public static ParsedTriggerName? Parse(
        string name,
        string prefix,
        string tableName,
        IReadOnlyList<string> eventTokens
    )
    {
        var suffix = $"_{tableName}";
        if (
            !name.StartsWith(prefix, StringComparison.Ordinal)
            || !name.EndsWith(suffix, StringComparison.Ordinal)
        )
        {
            return null;
        }

        var body = name[prefix.Length..^suffix.Length];
        foreach (var token in eventTokens)
        {
            if (body.StartsWith($"{token}_", StringComparison.Ordinal))
            {
                return new ParsedTriggerName(token, body[(token.Length + 1)..]);
            }
        }
        return null;
    }
}

/// <summary>Trigger name split into its event token and base name.</summary>
internal sealed record ParsedTriggerName(string EventToken, string BaseName);
