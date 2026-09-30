using YamlDotNet.Serialization;

namespace Nimblesite.DataProvider.Migration.Core;

// Implements [MIG-TRIGGER-MODEL] from docs/specs/declarative-triggers-spec.md
// (GitHub issue 82).

/// <summary>
/// Declarative trigger guard attached to a table. The trigger raises
/// <see cref="ErrorMessage"/> and aborts the statement when
/// <see cref="RaiseWhenLql"/> evaluates true for the affected row. Diffed by
/// name and materialised by the platform DDL generator, like RLS policies.
/// </summary>
public sealed record TriggerDefinition
{
    /// <summary>Trigger name -- unique within the table.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>When the trigger fires relative to the event. Default Before.</summary>
    public TriggerTiming Timing { get; init; } = TriggerTiming.Before;

    /// <summary>Events the trigger fires on. At least one is required.</summary>
    public IReadOnlyList<TriggerEvent> Events { get; init; } = [];

    /// <summary>True to fire once per affected row (default).</summary>
    [YamlMember(DefaultValuesHandling = DefaultValuesHandling.Preserve)]
    public bool ForEachRow { get; init; } = true;

    /// <summary>
    /// LQL guard predicate. Row column references use <c>old.</c>/<c>new.</c>
    /// prefixes; supports <c>and</c>/<c>or</c> composition and
    /// <c>[not] exists(pipeline)</c> subqueries. Implements
    /// [MIG-TRIGGER-GUARD-LQL].
    /// </summary>
    [YamlMember(Alias = "raiseWhen")]
    public string? RaiseWhenLql { get; init; }

    /// <summary>Error message raised when the guard predicate holds.</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// When a trigger fires relative to its event.
/// </summary>
public enum TriggerTiming
{
    /// <summary>Fires before the row change is applied.</summary>
    Before,

    /// <summary>Fires after the row change is applied.</summary>
    After,
}

/// <summary>
/// DML event a trigger fires on.
/// </summary>
public enum TriggerEvent
{
    /// <summary>Fires on <c>INSERT</c>.</summary>
    Insert,

    /// <summary>Fires on <c>UPDATE</c>.</summary>
    Update,

    /// <summary>Fires on <c>DELETE</c>.</summary>
    Delete,
}
