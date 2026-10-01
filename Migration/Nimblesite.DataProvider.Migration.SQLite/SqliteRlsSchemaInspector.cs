namespace Nimblesite.DataProvider.Migration.SQLite;

// Implements [RLS-DIFF] SQLite trigger reverse-map support. Trigger bodies and
// the secure view are parsed by <see cref="SqliteRlsPredicateReader"/> so the
// inspected policies carry the live USING / WITH CHECK predicates; SchemaDiff
// can then detect and replace a changed predicate (issue #98).

internal static class SqliteRlsSchemaInspector
{
    public static RlsPolicySetDefinition? Inspect(SqliteConnection connection, string tableName)
    {
        var triggers = SqliteTriggerNames.ReadDefinitions(
            connection,
            tableName,
            $"rls_%_{tableName}"
        );
        if (triggers.Count == 0)
        {
            return null;
        }

        var secureView = SqliteRlsPredicateReader.ReadSecureView(connection, tableName);
        var policies = triggers
            .Select(trigger =>
                (
                    Trigger: trigger,
                    Parsed: SqliteTriggerNames.Parse(
                        trigger.Name,
                        "rls_",
                        tableName,
                        SqliteTriggerNames.DmlEventTokens
                    )
                )
            )
            .Where(item => item.Parsed is not null)
            .GroupBy(item => item.Parsed!.BaseName, StringComparer.OrdinalIgnoreCase)
            .Select(group => ToPolicy(group.Key, group, secureView))
            .OrderBy(policy => policy.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return policies.Count == 0 ? null : new RlsPolicySetDefinition { Policies = policies };
    }

    private static RlsPolicyDefinition ToPolicy(
        string name,
        IEnumerable<(ManagedTriggerDefinition Trigger, ParsedTriggerName Parsed)> group,
        (bool Exists, string? Predicate) secureView
    )
    {
        var members = group.ToList();
        var (usingSql, withCheckSql) = ExtractPredicates(members, secureView);
        var operations = members
            .Select(member => ToOperation(member.Parsed.EventToken))
            .Distinct()
            .OrderBy(operation => (int)operation)
            .ToList();
        return new RlsPolicyDefinition
        {
            Name = name,
            Operations = CoversAllOperations(operations) ? [RlsOperation.All] : operations,
            UsingSql = usingSql,
            WithCheckSql = withCheckSql,
        };
    }

    // A generated ALL policy materialises as the secure view plus insert,
    // update, and delete triggers. Predicates are attached only when every
    // managed object parsed; with any unreadable object they stay null, which
    // SchemaDiff treats as "cannot verify" rather than drift.
    private static (string? Using, string? WithCheck) ExtractPredicates(
        List<(ManagedTriggerDefinition Trigger, ParsedTriggerName Parsed)> members,
        (bool Exists, string? Predicate) secureView
    )
    {
        string? usingSql = null;
        string? withCheckSql = null;
        foreach (var member in members)
        {
            if (SqliteRlsPredicateReader.TriggerPredicate(member.Trigger.Sql) is not { } predicate)
            {
                return (null, null);
            }
            if (member.Parsed.EventToken == "delete")
            {
                usingSql = predicate;
            }
            else
            {
                withCheckSql = predicate;
            }
        }
        if (secureView.Exists)
        {
            if (secureView.Predicate is not { } viewPredicate)
            {
                return (null, null);
            }
            usingSql = viewPredicate;
        }
        return (usingSql, withCheckSql);
    }

    private static bool CoversAllOperations(List<RlsOperation> operations) =>
        operations.Count == 4
        && operations.Contains(RlsOperation.Select)
        && operations.Contains(RlsOperation.Insert)
        && operations.Contains(RlsOperation.Update)
        && operations.Contains(RlsOperation.Delete);

    private static RlsOperation ToOperation(string eventToken) =>
        eventToken switch
        {
            "insert" => RlsOperation.Insert,
            "update" => RlsOperation.Update,
            _ => RlsOperation.Delete,
        };
}
