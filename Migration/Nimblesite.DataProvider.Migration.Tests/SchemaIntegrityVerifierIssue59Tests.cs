using System.Collections.Immutable;
using SchemaIntegrityOk = Outcome.Result<
    System.Collections.Immutable.ImmutableArray<string>,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Ok<
    System.Collections.Immutable.ImmutableArray<string>,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>;

namespace Nimblesite.DataProvider.Migration.Tests;

/// <summary>
/// Implements [MIG-VERIFY-DEFAULTS-AND-GRANTS]: the schema integrity verifier
/// must accept any live schema that satisfies the desired one, regardless of
/// how the per-provider inspector represents it. Closes #59:
///   1. A platform may read back a string default with an explicit type suffix
///      (e.g. <c>'pending'::text</c>) while YAML declares the bare literal
///      <c>'pending'</c>.
///   2. An inspector may emit grants in atomic per-role form, while YAML can
///      list multiple roles per grant.
///   3. An inspector may not emit <c>AllTablesInSchema</c> directly even when
///      YAML declares it.
/// </summary>
public sealed class SchemaIntegrityVerifierIssue59Tests
{
    [Fact]
    public void Verify_TextDefaultStoredWithTypeCast_TreatedAsEqualToBareLiteral()
    {
        var live = new SchemaDefinition
        {
            Name = "live",
            Tables =
            [
                new TableDefinition
                {
                    Schema = "public",
                    Name = "t",
                    Columns =
                    [
                        new ColumnDefinition
                        {
                            Name = "status",
                            Type = PortableTypes.Text,
                            IsNullable = false,
                            DefaultValue = "'pending'::text",
                        },
                    ],
                },
            ],
        };
        var desired = new SchemaDefinition
        {
            Name = "desired",
            Tables =
            [
                new TableDefinition
                {
                    Schema = "public",
                    Name = "t",
                    Columns =
                    [
                        new ColumnDefinition
                        {
                            Name = "status",
                            Type = PortableTypes.Text,
                            IsNullable = false,
                            DefaultValue = "'pending'",
                        },
                    ],
                },
            ],
        };

        var mismatches = Verify(live: live, desired: desired);

        Assert.Empty(mismatches);

        var changedDefault = new SchemaDefinition
        {
            Name = "changed-default",
            Tables =
            [
                new TableDefinition
                {
                    Schema = "public",
                    Name = "t",
                    Columns =
                    [
                        new ColumnDefinition
                        {
                            Name = "status",
                            Type = PortableTypes.Text,
                            IsNullable = false,
                            DefaultValue = "'failed'",
                        },
                    ],
                },
            ],
        };

        var changedDefaultMismatches = Verify(live: live, desired: changedDefault);

        Assert.Single(changedDefaultMismatches);
        Assert.Contains("public.t.status: default expected 'failed'", changedDefaultMismatches[0]);
        Assert.DoesNotContain("default expected 'pending'", changedDefaultMismatches[0]);
    }

    [Fact]
    public void Verify_MultiRoleGrantInDesired_MatchedAgainstSingleRoleGrantsFromInspector()
    {
        var live = new SchemaDefinition
        {
            Name = "live",
            Grants =
            [
                new PostgresGrantDefinition
                {
                    Schema = "public",
                    Target = PostgresGrantTarget.Schema,
                    Privileges = ["USAGE"],
                    Roles = ["app_user"],
                },
                new PostgresGrantDefinition
                {
                    Schema = "public",
                    Target = PostgresGrantTarget.Schema,
                    Privileges = ["USAGE"],
                    Roles = ["app_admin"],
                },
            ],
        };
        var desired = new SchemaDefinition
        {
            Name = "desired",
            Grants =
            [
                new PostgresGrantDefinition
                {
                    Schema = "public",
                    Target = PostgresGrantTarget.Schema,
                    Privileges = ["USAGE"],
                    Roles = ["app_user", "app_admin"],
                },
            ],
        };

        var mismatches = Verify(live: live, desired: desired);

        Assert.Empty(mismatches);

        var missingAdminGrant = new SchemaDefinition
        {
            Name = "missing-admin-grant",
            Grants = [live.Grants[0]],
        };

        var missingAdminMismatches = Verify(live: missingAdminGrant, desired: desired);

        Assert.Single(missingAdminMismatches);
        Assert.Contains("grant Schema", missingAdminMismatches[0]);
        Assert.Contains("missing grant", missingAdminMismatches[0]);
    }

    [Fact]
    public void Verify_AllTablesInSchemaTarget_MatchedAgainstPerTableGrantsFromInspector()
    {
        var live = new SchemaDefinition
        {
            Name = "live",
            Tables =
            [
                new TableDefinition { Schema = "public", Name = "a" },
                new TableDefinition { Schema = "public", Name = "b" },
            ],
            Grants =
            [
                new PostgresGrantDefinition
                {
                    Schema = "public",
                    Target = PostgresGrantTarget.Table,
                    ObjectName = "a",
                    Privileges = ["SELECT", "INSERT"],
                    Roles = ["app_user"],
                },
                new PostgresGrantDefinition
                {
                    Schema = "public",
                    Target = PostgresGrantTarget.Table,
                    ObjectName = "b",
                    Privileges = ["SELECT", "INSERT"],
                    Roles = ["app_user"],
                },
            ],
        };
        var desired = new SchemaDefinition
        {
            Name = "desired",
            Tables =
            [
                new TableDefinition { Schema = "public", Name = "a" },
                new TableDefinition { Schema = "public", Name = "b" },
            ],
            Grants =
            [
                new PostgresGrantDefinition
                {
                    Schema = "public",
                    Target = PostgresGrantTarget.AllTablesInSchema,
                    Privileges = ["SELECT", "INSERT"],
                    Roles = ["app_user"],
                },
            ],
        };

        var mismatches = Verify(live: live, desired: desired);

        Assert.Empty(mismatches);

        var missingTableGrant = new SchemaDefinition
        {
            Name = "missing-table-grant",
            Tables = live.Tables,
            Grants = [live.Grants[0]],
        };

        var missingTableMismatches = Verify(live: missingTableGrant, desired: desired);

        Assert.Single(missingTableMismatches);
        Assert.Contains("grant AllTablesInSchema", missingTableMismatches[0]);
        Assert.Contains("missing grant", missingTableMismatches[0]);
    }

    private static ImmutableArray<string> Verify(SchemaDefinition live, SchemaDefinition desired)
    {
        var result = SchemaIntegrityVerifier.Verify(
            live: live,
            desired: desired,
            logger: NullLogger.Instance
        );

        Assert.True(
            result is SchemaIntegrityOk,
            "Expected schema integrity verification to succeed."
        );
        return ((SchemaIntegrityOk)result).Value;
    }
}
