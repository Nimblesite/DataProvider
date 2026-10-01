using TranspileError = Outcome.Result<
    string,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Error<string, Nimblesite.DataProvider.Migration.Core.MigrationError>;
using TranspileOk = Outcome.Result<
    string,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Ok<string, Nimblesite.DataProvider.Migration.Core.MigrationError>;

namespace Nimblesite.DataProvider.Migration.SQLite;

// Implements [RLS-SQLITE].

internal static class SqliteRlsDdlBuilder
{
    public static string GenerateEnable() =>
        "CREATE TABLE IF NOT EXISTS [__rls_context] ([current_user_id] TEXT NOT NULL)";

    public static string GenerateCreatePolicy(CreateRlsPolicyOperation op) =>
        PolicyDdl(op.Policy, op.TableName);

    // Implements [RLS-DIFF] (issue #98): SQLite triggers and views cannot be
    // altered in place, so a changed policy is dropped and recreated. Runs in
    // the migration transaction (ReplaceRlsPolicyOperation).
    public static string GenerateReplacePolicy(ReplaceRlsPolicyOperation op) =>
        string.Join(
            ";\n",
            DropObjects(op.PolicyName, op.TableName).Append(PolicyDdl(op.Policy, op.TableName))
        );

    public static string GenerateDropPolicy(DropRlsPolicyOperation op) =>
        string.Join(
            ";\n",
            Operations()
                .Where(sqlOp => sqlOp != "select")
                .Select(sqlOp =>
                    $"DROP TRIGGER IF EXISTS [{TriggerName(sqlOp, op.PolicyName, op.TableName)}]"
                )
        );

    private static IEnumerable<string> DropObjects(string policyName, string tableName)
    {
        foreach (var sqlOp in Operations().Where(sqlOp => sqlOp != "select"))
        {
            yield return $"DROP TRIGGER IF EXISTS [{TriggerName(sqlOp, policyName, tableName)}]";
        }
        yield return $"DROP VIEW IF EXISTS [{tableName}_secure]";
    }

    private static string PolicyDdl(RlsPolicyDefinition policy, string tableName)
    {
        var ddl = new List<string>();
        AddRestrictiveWarning(policy, ddl);
        AddInsertTrigger(policy, tableName, ddl);
        AddUpdateTrigger(policy, tableName, ddl);
        AddDeleteTrigger(policy, tableName, ddl);
        AddSecureView(policy, tableName, ddl);
        return string.Join(";\n", ddl);
    }

    public static string GenerateDisable(DisableRlsOperation op) =>
        $"DROP VIEW IF EXISTS [{op.TableName}_secure]";

    private static void AddRestrictiveWarning(RlsPolicyDefinition policy, List<string> ddl)
    {
        if (!policy.IsPermissive)
        {
            ddl.Add("-- MIG-W-RLS-SQLITE-RESTRICTIVE-APPROX");
        }
    }

    private static void AddInsertTrigger(
        RlsPolicyDefinition policy,
        string tableName,
        List<string> ddl
    )
    {
        if (Applies(policy, RlsOperation.Insert) && HasText(policy.WithCheckLql))
        {
            ddl.Add(Trigger(policy, tableName, "insert", "INSERT", "NEW", policy.WithCheckLql!));
        }
    }

    private static void AddUpdateTrigger(
        RlsPolicyDefinition policy,
        string tableName,
        List<string> ddl
    )
    {
        if (Applies(policy, RlsOperation.Update) && HasText(policy.WithCheckLql))
        {
            ddl.Add(Trigger(policy, tableName, "update", "UPDATE", "NEW", policy.WithCheckLql!));
        }
    }

    private static void AddDeleteTrigger(
        RlsPolicyDefinition policy,
        string tableName,
        List<string> ddl
    )
    {
        if (Applies(policy, RlsOperation.Delete) && HasText(policy.UsingLql))
        {
            ddl.Add(Trigger(policy, tableName, "delete", "DELETE", "OLD", policy.UsingLql!));
        }
    }

    private static void AddSecureView(
        RlsPolicyDefinition policy,
        string tableName,
        List<string> ddl
    )
    {
        if (Applies(policy, RlsOperation.Select) && HasText(policy.UsingLql))
        {
            var predicate = Translate(policy.UsingLql!, policy.Name);
            ddl.Add(
                $"CREATE VIEW IF NOT EXISTS [{tableName}_secure] AS SELECT * FROM [{tableName}] WHERE {predicate}"
            );
        }
    }

    private static string Trigger(
        RlsPolicyDefinition policy,
        string tableName,
        string sqlOp,
        string verb,
        string rowAlias,
        string lql
    )
    {
        var predicate = PrefixRowColumns(Translate(lql, policy.Name), rowAlias);
        var name = TriggerName(sqlOp, policy.Name, tableName);
        return $"""
            CREATE TRIGGER IF NOT EXISTS [{name}]
            BEFORE {verb} ON [{tableName}]
            BEGIN
              SELECT RAISE(ABORT, 'RLS-SQLITE: access denied [{policy.Name}]')
              WHERE NOT ({predicate});
            END
            """;
    }

    private static string Translate(string lql, string policyName)
    {
        var result = RlsPredicateTranspiler.Translate(lql, RlsPlatform.Sqlite, policyName);
        return result switch
        {
            TranspileOk ok => ok.Value,
            TranspileError error => throw new InvalidOperationException(error.Value.Message),
        };
    }

    private static string PrefixRowColumns(string sql, string rowAlias)
    {
        var sb = new StringBuilder(sql.Length + 16);
        for (var i = 0; i < sql.Length; i++)
        {
            if (sql[i] == '[')
            {
                i = AppendBracketIdentifier(sql, i, rowAlias, sb);
                continue;
            }
            sb.Append(sql[i]);
        }
        return sb.ToString();
    }

    private static int AppendBracketIdentifier(
        string sql,
        int start,
        string rowAlias,
        StringBuilder sb
    )
    {
        var end = sql.IndexOf(']', start + 1);
        if (end < 0)
        {
            sb.Append(sql[start..]);
            return sql.Length;
        }
        var name = sql[(start + 1)..end];
        sb.Append(ShouldPrefix(sql, start, name) ? $"{rowAlias}.[{name}]" : $"[{name}]");
        return end;
    }

    private static bool ShouldPrefix(string sql, int start, string name) =>
        !name.Equals("__rls_context", StringComparison.Ordinal) && !IsQualified(sql, start);

    private static bool IsQualified(string sql, int start)
    {
        var prev = start - 1;
        while (prev >= 0 && char.IsWhiteSpace(sql[prev]))
        {
            prev--;
        }
        return prev >= 0 && sql[prev] == '.';
    }

    private static bool Applies(RlsPolicyDefinition policy, RlsOperation op) =>
        policy.Operations.Count == 0
        || policy.Operations.Contains(RlsOperation.All)
        || policy.Operations.Contains(op);

    private static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);

    private static IEnumerable<string> Operations() => ["insert", "update", "delete", "select"];

    private static string TriggerName(string sqlOp, string policyName, string tableName) =>
        $"rls_{sqlOp}_{policyName}_{tableName}";
}
