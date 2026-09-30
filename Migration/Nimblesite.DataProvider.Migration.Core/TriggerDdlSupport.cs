using GuardTranspileError = Outcome.Result<
    string,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Error<string, Nimblesite.DataProvider.Migration.Core.MigrationError>;
using GuardTranspileOk = Outcome.Result<
    string,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Ok<string, Nimblesite.DataProvider.Migration.Core.MigrationError>;

namespace Nimblesite.DataProvider.Migration.Core;

// Implements [MIG-TRIGGER-SQLITE] and [MIG-TRIGGER-PG] shared DDL support
// from docs/specs/declarative-triggers-spec.md (GitHub issue 82).

/// <summary>
/// Shared helpers for platform trigger DDL builders: guard predicate
/// validation and translation, error message defaulting/escaping, and
/// keyword mapping. All failures throw, matching the established
/// DDL-generator pattern; <c>MigrationRunner</c> converts them to error
/// results.
/// </summary>
public static class TriggerDdlSupport
{
    /// <summary>
    /// Name prefix identifying migration-managed trigger objects on every
    /// platform. Inspectors ignore triggers without this prefix so
    /// destructive runs never drop triggers created outside the migration
    /// tool (e.g. Sync change tracking).
    /// </summary>
    public const string ManagedTriggerPrefix = "usr_";

    /// <summary>
    /// Name suffix identifying PostgreSQL trigger guard functions. These are
    /// owned by the trigger lifecycle and excluded from support-function
    /// read-back so destructive runs do not try to drop them independently.
    /// </summary>
    public const string GuardFunctionSuffix = "_trgfn";

    /// <summary>
    /// Validates the trigger declaration and translates its guard predicate
    /// for the target platform. Throws <see cref="InvalidOperationException"/>
    /// on invalid declarations and <see cref="NotSupportedException"/> for
    /// statement-level triggers.
    /// </summary>
    public static string BuildGuardPredicate(TriggerDefinition trigger, RlsPlatform platform)
    {
        RequireForEachRow(trigger);
        var lql = RequireRaiseWhen(trigger);
        ValidateRowReferences(trigger, lql, RequireEvents(trigger));
        var result = RlsPredicateTranspiler.TranslateGuardPredicate(lql, platform, trigger.Name);
        return result switch
        {
            GuardTranspileOk ok => ok.Value,
            GuardTranspileError error => throw new InvalidOperationException(error.Value.Message),
        };
    }

    /// <summary>Returns the distinct declared events or throws when empty.</summary>
    public static IReadOnlyList<TriggerEvent> RequireEvents(TriggerDefinition trigger) =>
        trigger.Events.Count > 0
            ? trigger.Events.Distinct().ToList()
            : throw new InvalidOperationException($"Trigger '{trigger.Name}' declares no events");

    /// <summary>
    /// Error message (defaulted from the trigger name when absent) with
    /// single quotes escaped for embedding in a SQL string literal.
    /// </summary>
    public static string EscapedMessage(TriggerDefinition trigger)
    {
        var message = string.IsNullOrWhiteSpace(trigger.ErrorMessage)
            ? $"TRIGGER-GUARD: {trigger.Name}"
            : trigger.ErrorMessage;
        return message.Replace("'", "''", StringComparison.Ordinal);
    }

    /// <summary>SQL verb for a trigger event.</summary>
    public static string EventVerb(TriggerEvent triggerEvent) =>
        triggerEvent switch
        {
            TriggerEvent.Insert => "INSERT",
            TriggerEvent.Update => "UPDATE",
            TriggerEvent.Delete => "DELETE",
            _ => throw new NotSupportedException($"Unknown trigger event: {triggerEvent}"),
        };

    /// <summary>SQL timing keyword for a trigger timing.</summary>
    public static string TimingKeyword(TriggerTiming timing) =>
        timing == TriggerTiming.After ? "AFTER" : "BEFORE";

    private static string RequireRaiseWhen(TriggerDefinition trigger) =>
        trigger.RaiseWhenLql is { } lql && !string.IsNullOrWhiteSpace(lql)
            ? lql
            : throw new InvalidOperationException(
                $"Trigger '{trigger.Name}' has no raiseWhen predicate"
            );

    /// <summary>
    /// Guards are row-level on every platform. SQLite has no statement-level
    /// triggers and Postgres statement-level triggers see NULL OLD/NEW, which
    /// would silently disable the guard -- so <c>forEachRow: false</c> fails
    /// loudly instead of diverging per platform.
    /// </summary>
    private static void RequireForEachRow(TriggerDefinition trigger)
    {
        if (!trigger.ForEachRow)
        {
            throw new NotSupportedException(
                $"Trigger '{trigger.Name}': forEachRow: false (statement-level) is not supported; guards are row-level on every platform"
            );
        }
    }

    /// <summary>
    /// Rejects row references that do not exist for a declared event
    /// (<c>old.</c> on INSERT, <c>new.</c> on DELETE). Without this, SQLite
    /// fails at DML time while Postgres silently never fires the guard.
    /// </summary>
    private static void ValidateRowReferences(
        TriggerDefinition trigger,
        string lql,
        IReadOnlyList<TriggerEvent> events
    )
    {
        if (
            events.Contains(TriggerEvent.Insert)
            && RlsPredicateTranspiler.GuardReferencesRow(lql, "old")
        )
        {
            throw new InvalidOperationException(
                $"Trigger '{trigger.Name}' references old. but fires on Insert; OLD row values do not exist for INSERT"
            );
        }
        if (
            events.Contains(TriggerEvent.Delete)
            && RlsPredicateTranspiler.GuardReferencesRow(lql, "new")
        )
        {
            throw new InvalidOperationException(
                $"Trigger '{trigger.Name}' references new. but fires on Delete; NEW row values do not exist for DELETE"
            );
        }
    }
}
