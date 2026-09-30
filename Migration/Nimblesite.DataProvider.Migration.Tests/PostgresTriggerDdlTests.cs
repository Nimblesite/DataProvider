namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [MIG-TRIGGER-PG] DDL emission rules from
// docs/specs/declarative-triggers-spec.md (GitHub issue 82).

/// <summary>
/// Generator-level tests for PostgreSQL trigger guard DDL emission rules
/// that do not need a live database.
/// </summary>
public sealed class PostgresTriggerDdlTests
{
    [Fact]
    public void PgTriggerDdl_DollarTagCollision_PicksAlternateTag()
    {
        var ddl = PostgresDdlGenerator.Generate(
            new CreateTriggerOperation(
                "public",
                "tenant_members",
                Guard() with
                {
                    ErrorMessage = "do not $trigger_guard$ terminate me",
                }
            )
        );

        Assert.Contains("$trigger_guard1$", ddl, StringComparison.Ordinal);
        Assert.Contains("do not $trigger_guard$ terminate me", ddl, StringComparison.Ordinal);
    }

    [Fact]
    public void PgTriggerDdl_ManagedPrefix_AppliedToTriggerObjectName()
    {
        var ddl = PostgresDdlGenerator.Generate(
            new CreateTriggerOperation("public", "tenant_members", Guard())
        );

        Assert.Contains(
            "CREATE TRIGGER \"usr_assert_not_last_owner\"",
            ddl,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void PgTriggerDdl_IdentifierOver63Bytes_FailsLoudly()
    {
        // PostgreSQL silently truncates identifiers to 63 bytes, which would
        // desynchronise create/drop/inspect names.
        var ex = Assert.Throws<InvalidOperationException>(() =>
            PostgresDdlGenerator.Generate(
                new CreateTriggerOperation(
                    "public",
                    "tenant_members",
                    Guard() with
                    {
                        Name = new string('x', 80),
                    }
                )
            )
        );

        Assert.Contains("63-byte", ex.Message, StringComparison.Ordinal);
    }

    private static TriggerDefinition Guard() =>
        new()
        {
            Name = "assert_not_last_owner",
            Events = [TriggerEvent.Delete],
            RaiseWhenLql = "old.role = 'owner'",
            ErrorMessage = "cannot remove the last owner of a tenant",
        };
}
