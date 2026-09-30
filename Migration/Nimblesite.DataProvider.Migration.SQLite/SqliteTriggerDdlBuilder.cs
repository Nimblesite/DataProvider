namespace Nimblesite.DataProvider.Migration.SQLite;

// Implements [MIG-TRIGGER-SQLITE] from docs/specs/declarative-triggers-spec.md
// (GitHub issue 82).

/// <summary>
/// Emits SQLite DDL for declarative trigger guards. One trigger per declared
/// event, named <c>usr_{event}_{trigger}_{table}</c> so the inspector can
/// read the definition back by name. SQLite only supports row-level
/// triggers, so <c>FOR EACH ROW</c> is always emitted.
/// </summary>
internal static class SqliteTriggerDdlBuilder
{
    public static string GenerateCreate(CreateTriggerOperation op)
    {
        var predicate = TriggerDdlSupport.BuildGuardPredicate(op.Trigger, RlsPlatform.Sqlite);
        var message = TriggerDdlSupport.EscapedMessage(op.Trigger);
        return string.Join(
            ";\n",
            TriggerDdlSupport
                .RequireEvents(op.Trigger)
                .Select(triggerEvent => Trigger(op, triggerEvent, predicate, message))
        );
    }

    public static string GenerateDrop(DropTriggerOperation op) =>
        string.Join(
            ";\n",
            SqliteTriggerNames.DmlEventTokens.Select(token =>
                $"DROP TRIGGER IF EXISTS [{TriggerName(token, op.TriggerName, op.TableName)}]"
            )
        );

    private static string Trigger(
        CreateTriggerOperation op,
        TriggerEvent triggerEvent,
        string predicate,
        string message
    )
    {
        var verb = TriggerDdlSupport.EventVerb(triggerEvent);
        var name = TriggerName(verb.ToLowerInvariant(), op.Trigger.Name, op.TableName);
        return $"""
            CREATE TRIGGER IF NOT EXISTS [{name}]
            {TriggerDdlSupport.TimingKeyword(op.Trigger.Timing)} {verb} ON [{op.TableName}]
            FOR EACH ROW
            BEGIN
              SELECT RAISE(ABORT, '{message}')
              WHERE {predicate};
            END
            """;
    }

    private static string TriggerName(string eventToken, string triggerName, string tableName) =>
        $"{TriggerDdlSupport.ManagedTriggerPrefix}{eventToken}_{triggerName}_{tableName}";
}
