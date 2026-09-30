namespace Nimblesite.DataProvider.Migration.SQLite;

// Implements [RLS-DIFF] SQLite trigger reverse-map support.

internal static class SqliteRlsSchemaInspector
{
    public static RlsPolicySetDefinition? Inspect(SqliteConnection connection, string tableName)
    {
        var triggers = SqliteTriggerNames.Read(connection, tableName, $"rls_%_{tableName}");
        if (triggers.Count == 0)
        {
            return null;
        }

        var policies = triggers
            .Select(t => ToTriggerPolicy(t, tableName))
            .OfType<SqliteRlsTriggerPolicy>()
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(ToPolicy)
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return policies.Count == 0 ? null : new RlsPolicySetDefinition { Policies = policies };
    }

    private static SqliteRlsTriggerPolicy? ToTriggerPolicy(string name, string tableName)
    {
        var parsed = SqliteTriggerNames.Parse(
            name,
            "rls_",
            tableName,
            SqliteTriggerNames.DmlEventTokens
        );
        return parsed is null
            ? null
            : new SqliteRlsTriggerPolicy(parsed.BaseName, ToOperation(parsed.EventToken));
    }

    private static RlsOperation ToOperation(string eventToken) =>
        eventToken switch
        {
            "insert" => RlsOperation.Insert,
            "update" => RlsOperation.Update,
            _ => RlsOperation.Delete,
        };

    private static RlsPolicyDefinition ToPolicy(IGrouping<string, SqliteRlsTriggerPolicy> group) =>
        new() { Name = group.Key, Operations = group.Select(p => p.Operation).Distinct().ToList() };
}

internal sealed record SqliteRlsTriggerPolicy(string Name, RlsOperation Operation);
