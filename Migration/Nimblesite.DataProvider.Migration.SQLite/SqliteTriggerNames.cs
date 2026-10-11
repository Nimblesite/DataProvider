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
        return ReadManaged<string>(
            connection: connection,
            tableName: tableName,
            pattern: pattern,
            setSql: command => command.CommandText = SqliteSchemaInspector.SqlSpecification3,
            read: reader => reader.GetString(0)
        );
    }

    /// <summary>
    /// Read managed triggers with their stored <c>sqlite_master.sql</c> text so
    /// callers can inspect trigger bodies (for example RLS predicates).
    /// </summary>
    public static List<ManagedTriggerDefinition> ReadDefinitions(
        SqliteConnection connection,
        string tableName,
        string pattern
    )
    {
        return ReadManaged<ManagedTriggerDefinition>(
            connection: connection,
            tableName: tableName,
            pattern: pattern,
            setSql: command => command.CommandText = SqliteSchemaInspector.SqlSpecification4,
            read: reader => new ManagedTriggerDefinition(
                Name: reader.GetString(0),
                Sql: reader.GetString(1)
            )
        );
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

    // Implements [MIG-SQLITE-SHARED-MANAGED-TRIGGER-READS].
    private static List<T> ReadManaged<T>(
        SqliteConnection connection,
        string tableName,
        string pattern,
        Action<SqliteCommand> setSql,
        Func<SqliteDataReader, T> read
    )
    {
        using var command = connection.CreateCommand();
        setSql(obj: command);
        command.Parameters.AddWithValue(parameterName: "@table", value: tableName);
        command.Parameters.AddWithValue(parameterName: "@pattern", value: pattern);
        using var reader = command.ExecuteReader();
        var rows = new List<T>();
        while (reader.Read())
            rows.Add(item: read(arg: reader));
        return rows;
    }
}

/// <summary>Trigger name split into its event token and base name.</summary>
internal sealed record ParsedTriggerName(string EventToken, string BaseName);

/// <summary>A managed trigger together with its stored CREATE TRIGGER text.</summary>
internal sealed record ManagedTriggerDefinition(string Name, string Sql);
