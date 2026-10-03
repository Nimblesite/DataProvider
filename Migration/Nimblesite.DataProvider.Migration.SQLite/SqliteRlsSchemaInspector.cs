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
        var secureView = SqliteRlsPredicateReader.ReadSecureView(connection, tableName);
        var policies = triggers
            .SelectMany(trigger =>
                SqliteTriggerNames.Parse(
                    trigger.Name,
                    "rls_",
                    tableName,
                    SqliteTriggerNames.DmlEventTokens
                )
                    is { } parsed
                    ? new[] { (Trigger: trigger, Parsed: parsed) }
                    : []
            )
            .GroupBy(item => item.Parsed.BaseName, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
                ToPolicy(
                    group.Key,
                    group,
                    secureView.PolicyName is null || secureView.PolicyName == group.Key
                        ? secureView
                        : (false, null, null)
                )
            )
            .OrderBy(policy => policy.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (
            secureView.Exists
            && !policies.Any(policy =>
                policy.Operations.Contains(RlsOperation.Select)
                || policy.Operations.Contains(RlsOperation.All)
            )
        )
        {
            policies.Add(
                ToPolicy(
                    name: secureView.PolicyName ?? $"{tableName}_secure",
                    group: [],
                    secureView: secureView
                )
            );
        }

        return policies.Count == 0 ? null : new RlsPolicySetDefinition { Policies = policies };
    }

    private static RlsPolicyDefinition ToPolicy(
        string name,
        IEnumerable<(ManagedTriggerDefinition Trigger, ParsedTriggerName Parsed)> group,
        (bool Exists, string? Predicate, string? PolicyName) secureView
    )
    {
        var members = group.ToList();
        var (usingSql, withCheckSql) = ExtractPredicates(members, secureView);
        var operations = members
            .Select(member => ToOperation(member.Parsed.EventToken))
            .Concat(secureView.Exists ? [RlsOperation.Select] : [])
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
    // managed object parsed consistently; unreadable or conflicting predicates
    // stay null, which SchemaDiff treats as unverified drift and reconciles.
    private static (string? Using, string? WithCheck) ExtractPredicates(
        List<(ManagedTriggerDefinition Trigger, ParsedTriggerName Parsed)> members,
        (bool Exists, string? Predicate, string? PolicyName) secureView
    )
    {
        var predicates = members
            .Select(member =>
                (
                    Using: member.Parsed.EventToken == "delete",
                    Sql: SqliteRlsPredicateReader.TriggerPredicate(triggerSql: member.Trigger.Sql)
                )
            )
            .Concat(secureView.Exists ? [(Using: true, Sql: secureView.Predicate)] : [])
            .ToArray();
        var clauses = predicates.ToLookup(predicate => predicate.Using, predicate => predicate.Sql);
        var usingSql = clauses[true].ToArray();
        var withCheckSql = clauses[false].ToArray();
        return
            predicates.Any(predicate => predicate.Sql is null)
            || usingSql.Any(sql =>
                !RlsPolicyPredicates.SameSql(
                    left: usingSql[0],
                    right: sql,
                    platform: RlsPlatform.Sqlite
                )
            )
            || withCheckSql.Any(sql =>
                !RlsPolicyPredicates.SameSql(
                    left: withCheckSql[0],
                    right: sql,
                    platform: RlsPlatform.Sqlite
                )
            )
            ? (null, null)
            : (usingSql.FirstOrDefault(), withCheckSql.FirstOrDefault());
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
