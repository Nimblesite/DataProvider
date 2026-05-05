namespace Nimblesite.DataProvider.Migration.SQLite;

// Implements [RLS-DIFF] SQLite trigger reverse-map support.

internal static class SqliteRlsSchemaInspector
{
    public static RlsPolicySetDefinition? Inspect(SqliteConnection connection, string tableName)
    {
        var triggers = ReadTriggerNames(connection, tableName);
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

    private static List<string> ReadTriggerNames(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name FROM sqlite_master
            WHERE type = 'trigger' AND tbl_name = @table AND name LIKE @pattern
            ORDER BY name
            """;
        command.Parameters.AddWithValue("@table", tableName);
        command.Parameters.AddWithValue("@pattern", $"rls_%_{tableName}");
        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }
        return names;
    }

    private static SqliteRlsTriggerPolicy? ToTriggerPolicy(string name, string tableName)
    {
        var suffix = $"_{tableName}";
        if (
            !name.StartsWith("rls_", StringComparison.Ordinal)
            || !name.EndsWith(suffix, StringComparison.Ordinal)
        )
        {
            return null;
        }

        var body = name[4..^suffix.Length];
        var operation = ReadOperation(body);
        return operation is null
            ? null
            : new SqliteRlsTriggerPolicy(body[(operation.SqlName.Length + 1)..], operation);
    }

    private static SqliteRlsOperationName? ReadOperation(string body)
    {
        foreach (var op in OperationNames())
        {
            if (body.StartsWith($"{op.SqlName}_", StringComparison.Ordinal))
            {
                return op;
            }
        }
        return null;
    }

    private static RlsPolicyDefinition ToPolicy(IGrouping<string, SqliteRlsTriggerPolicy> group) =>
        new()
        {
            Name = group.Key,
            Operations = group.Select(p => p.Operation.RlsOperation).Distinct().ToList(),
        };

    private static IEnumerable<SqliteRlsOperationName> OperationNames() =>
        [
            new("insert", RlsOperation.Insert),
            new("update", RlsOperation.Update),
            new("delete", RlsOperation.Delete),
        ];
}

internal sealed record SqliteRlsTriggerPolicy(string Name, SqliteRlsOperationName Operation);

internal sealed record SqliteRlsOperationName(string SqlName, RlsOperation RlsOperation);
