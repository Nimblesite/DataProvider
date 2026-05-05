using YamlDotNet.Serialization;

namespace Nimblesite.DataProvider.Migration.Core;

// Implements [RLS-CORE-POLICY] from docs/specs/rls-spec.md.

/// <summary>
/// Row-level security policy set attached to a table.
/// When attached to <see cref="TableDefinition.RowLevelSecurity"/>, RLS is
/// enabled on the table and each contained policy is materialised by the
/// platform DDL generator.
/// </summary>
public sealed record RlsPolicySetDefinition
{
    /// <summary>
    /// True when row-level security is enabled on the table. False produces
    /// a <c>DisableRlsOperation</c> when previously enabled.
    /// </summary>
    [YamlMember(DefaultValuesHandling = DefaultValuesHandling.Preserve)]
    public bool Enabled { get; init; } = true;

    /// <summary>Policies attached to the table.</summary>
    public IReadOnlyList<RlsPolicyDefinition> Policies { get; init; } = [];

    /// <summary>
    /// True when <c>FORCE ROW LEVEL SECURITY</c> is set on the table.
    /// Postgres-only -- when forced, RLS additionally applies to the table
    /// owner. SQLite has no equivalent (its trigger emulation always
    /// applies). Implements GitHub issue #37.
    /// </summary>
    [YamlMember(Alias = "forced")]
    public bool Forced { get; init; }
}

/// <summary>
/// A single RLS policy. The predicate is expressed in LQL and transpiled to
/// platform-specific SQL by <c>RlsPredicateTranspiler</c> at DDL generation
/// time.
/// </summary>
public sealed record RlsPolicyDefinition
{
    /// <summary>Policy name -- unique within the table.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// True for <c>PERMISSIVE</c> policies (default). False for
    /// <c>RESTRICTIVE</c>. SQLite cannot distinguish these and emits a
    /// <c>MIG-W-RLS-SQLITE-RESTRICTIVE-APPROX</c> warning when restrictive
    /// policies are present.
    /// </summary>
    [YamlMember(Alias = "permissive", DefaultValuesHandling = DefaultValuesHandling.Preserve)]
    public bool IsPermissive { get; init; } = true;

    /// <summary>
    /// Operations the policy applies to. Defaults to <see cref="RlsOperation.All"/>.
    /// </summary>
    public IReadOnlyList<RlsOperation> Operations { get; init; } = [RlsOperation.All];

    /// <summary>
    /// Roles the policy applies to. Empty means <c>PUBLIC</c> (all roles).
    /// </summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>
    /// LQL predicate for the <c>USING</c> clause. Applied to <c>SELECT</c>,
    /// the existing-row side of <c>UPDATE</c>, and <c>DELETE</c>.
    /// </summary>
    [YamlMember(Alias = "using")]
    public string? UsingLql { get; init; }

    /// <summary>
    /// LQL predicate for the <c>WITH CHECK</c> clause. Applied to
    /// <c>INSERT</c> and the new-row side of <c>UPDATE</c>.
    /// </summary>
    [YamlMember(Alias = "withCheck")]
    public string? WithCheckLql { get; init; }

    /// <summary>
    /// Raw SQL escape hatch for the <c>USING</c> clause. Postgres-only; emitted
    /// verbatim. When set, takes precedence over <see cref="UsingLql"/>. Used
    /// when the predicate calls SECURITY DEFINER functions (e.g. <c>is_member()</c>)
    /// that cannot be expressed as LQL <c>exists()</c> subqueries because they
    /// would evaluate under the caller's RLS context. Implements GitHub issue #36.
    /// </summary>
    [YamlMember(Alias = "usingSql")]
    public string? UsingSql { get; init; }

    /// <summary>
    /// Raw SQL escape hatch for the <c>WITH CHECK</c> clause. Postgres-only.
    /// Implements GitHub issue #36.
    /// </summary>
    [YamlMember(Alias = "withCheckSql")]
    public string? WithCheckSql { get; init; }
}

/// <summary>
/// Operations an RLS policy can apply to. Mirrors PostgreSQL's
/// <c>FOR { ALL | SELECT | INSERT | UPDATE | DELETE }</c> clause.
/// </summary>
public enum RlsOperation
{
    /// <summary>Applies to all DML operations.</summary>
    All,

    /// <summary>Applies to <c>SELECT</c> only.</summary>
    Select,

    /// <summary>Applies to <c>INSERT</c> only.</summary>
    Insert,

    /// <summary>Applies to <c>UPDATE</c> only.</summary>
    Update,

    /// <summary>Applies to <c>DELETE</c> only.</summary>
    Delete,
}
