namespace Nimblesite.DataProvider.Migration.Postgres;

// Implements [MIG-TRIGGER-PG] from docs/specs/declarative-triggers-spec.md
// (GitHub issue 82).

/// <summary>
/// Emits PostgreSQL DDL for declarative trigger guards: one plpgsql guard
/// function (<c>{table}_{trigger}_trgfn</c>) that raises the declared error
/// when the guard predicate holds, plus one trigger named
/// <c>usr_{trigger}</c> wired to the declared events. The <c>usr_</c> prefix
/// marks the trigger as migration-managed so the inspector never reads (and
/// destructive runs never drop) triggers created outside the tool.
/// <c>RAISE ... USING MESSAGE</c> avoids format-string interpretation of
/// <c>%</c> in user messages, and the dollar-quote tag is chosen to never
/// collide with the function body.
/// </summary>
internal static class PostgresTriggerDdlBuilder
{
    private const int MaxIdentifierBytes = 63;

    public static string GenerateCreate(CreateTriggerOperation op)
    {
        var predicate = TriggerDdlSupport.BuildGuardPredicate(op.Trigger, RlsPlatform.Postgres);
        var message = TriggerDdlSupport.EscapedMessage(op.Trigger);
        var triggerName = ValidateIdentifier(ObjectName(op.Trigger.Name), "trigger name");
        var functionName = ValidateIdentifier(
            FunctionName(op.TableName, op.Trigger.Name),
            "trigger guard function name"
        );
        var events = string.Join(
            " OR ",
            TriggerDdlSupport.RequireEvents(op.Trigger).Select(TriggerDdlSupport.EventVerb)
        );
        var tag = DollarTag($"{predicate}\n{message}");
        return $"""
            CREATE OR REPLACE FUNCTION "{op.Schema}"."{functionName}"()
            RETURNS trigger
            LANGUAGE plpgsql
            AS {tag}
            BEGIN
              IF {predicate} THEN
                RAISE EXCEPTION USING MESSAGE = '{message}';
              END IF;
              IF TG_OP = 'DELETE' THEN
                RETURN OLD;
              END IF;
              RETURN NEW;
            END
            {tag};
            DROP TRIGGER IF EXISTS "{triggerName}" ON "{op.Schema}"."{op.TableName}";
            CREATE TRIGGER "{triggerName}"
            {TriggerDdlSupport.TimingKeyword(
                op.Trigger.Timing
            )} {events} ON "{op.Schema}"."{op.TableName}"
            FOR EACH ROW
            EXECUTE FUNCTION "{op.Schema}"."{functionName}"()
            """;
    }

    public static string GenerateDrop(DropTriggerOperation op) =>
        $"""
            DROP TRIGGER IF EXISTS "{ObjectName(op.TriggerName)}" ON "{op.Schema}"."{op.TableName}";
            DROP FUNCTION IF EXISTS "{op.Schema}"."{FunctionName(op.TableName, op.TriggerName)}"()
            """;

    private static string ObjectName(string triggerName) =>
        $"{TriggerDdlSupport.ManagedTriggerPrefix}{triggerName}";

    private static string FunctionName(string tableName, string triggerName) =>
        $"{tableName}_{triggerName}{TriggerDdlSupport.GuardFunctionSuffix}";

    /// <summary>
    /// PostgreSQL silently truncates identifiers to 63 bytes, which would
    /// desynchronise create/drop/inspect names -- fail loudly instead.
    /// </summary>
    private static string ValidateIdentifier(string name, string description) =>
        Encoding.UTF8.GetByteCount(name) <= MaxIdentifierBytes
            ? name
            : throw new InvalidOperationException(
                $"PostgreSQL {description} '{name}' exceeds the 63-byte identifier limit"
            );

    /// <summary>
    /// Picks a dollar-quote tag that does not occur in the function body so
    /// user-supplied predicates/messages can never terminate the quoting.
    /// </summary>
    private static string DollarTag(string body)
    {
        var suffix = 0;
        var tag = "$trigger_guard$";
        while (body.Contains(tag, StringComparison.Ordinal))
        {
            suffix++;
            tag = $"$trigger_guard{suffix}$";
        }
        return tag;
    }
}
