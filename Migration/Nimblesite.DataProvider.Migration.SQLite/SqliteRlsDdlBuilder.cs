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

    public static string GenerateCreatePolicy(CreateRlsPolicyOperation op)
    {
        var ddl = new List<string>();
        AddRestrictiveWarning(op.Policy, ddl);
        AddInsertTrigger(op, ddl);
        AddUpdateTrigger(op, ddl);
        AddDeleteTrigger(op, ddl);
        AddSecureView(op, ddl);
        return string.Join(";\n", ddl);
    }

    public static string GenerateDropPolicy(DropRlsPolicyOperation op) =>
        string.Join(
            ";\n",
            Operations()
                .Where(sqlOp => sqlOp != "select")
                .Select(sqlOp =>
                    $"DROP TRIGGER IF EXISTS [{TriggerName(sqlOp, op.PolicyName, op.TableName)}]"
                )
        );

    public static string GenerateDisable(DisableRlsOperation op) =>
        $"DROP VIEW IF EXISTS [{op.TableName}_secure]";

    private static void AddRestrictiveWarning(RlsPolicyDefinition policy, List<string> ddl)
    {
        if (!policy.IsPermissive)
        {
            ddl.Add("-- MIG-W-RLS-SQLITE-RESTRICTIVE-APPROX");
        }
    }

    private static void AddInsertTrigger(CreateRlsPolicyOperation op, List<string> ddl)
    {
        if (Applies(op.Policy, RlsOperation.Insert) && HasText(op.Policy.WithCheckLql))
        {
            ddl.Add(Trigger(op, "insert", "INSERT", "NEW", op.Policy.WithCheckLql!));
        }
    }

    private static void AddUpdateTrigger(CreateRlsPolicyOperation op, List<string> ddl)
    {
        if (Applies(op.Policy, RlsOperation.Update) && HasText(op.Policy.WithCheckLql))
        {
            ddl.Add(Trigger(op, "update", "UPDATE", "NEW", op.Policy.WithCheckLql!));
        }
    }

    private static void AddDeleteTrigger(CreateRlsPolicyOperation op, List<string> ddl)
    {
        if (Applies(op.Policy, RlsOperation.Delete) && HasText(op.Policy.UsingLql))
        {
            ddl.Add(Trigger(op, "delete", "DELETE", "OLD", op.Policy.UsingLql!));
        }
    }

    private static void AddSecureView(CreateRlsPolicyOperation op, List<string> ddl)
    {
        if (Applies(op.Policy, RlsOperation.Select) && HasText(op.Policy.UsingLql))
        {
            var predicate = Translate(op.Policy.UsingLql!, op.Policy.Name);
            ddl.Add(
                $"CREATE VIEW IF NOT EXISTS [{op.TableName}_secure] AS SELECT * FROM [{op.TableName}] WHERE {predicate}"
            );
        }
    }

    private static string Trigger(
        CreateRlsPolicyOperation op,
        string sqlOp,
        string verb,
        string rowAlias,
        string lql
    )
    {
        var predicate = PrefixRowColumns(Translate(lql, op.Policy.Name), rowAlias);
        var name = TriggerName(sqlOp, op.Policy.Name, op.TableName);
        return $"""
            CREATE TRIGGER IF NOT EXISTS [{name}]
            BEFORE {verb} ON [{op.TableName}]
            BEGIN
              SELECT RAISE(ABORT, 'RLS-SQLITE: access denied [{op.Policy.Name}]')
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
