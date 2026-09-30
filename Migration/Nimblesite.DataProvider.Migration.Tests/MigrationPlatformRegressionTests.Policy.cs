using System.Collections.Immutable;

namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial record MigrationPlatformRegressionTests
{
    private static PolicySnapshot ReadPolicy(MigrationTarget target) =>
        target.Provider switch
        {
            "postgres" => ReadPostgresPolicy(target),
            "sqlite" => ReadSqlitePolicy(target),
            "sqlserver" => ReadSqlServerPolicy(target),
            _ => UnsupportedPolicyProvider(target.Provider),
        };

    private static PolicySnapshot UnsupportedPolicyProvider(string provider)
    {
        Assert.Fail($"Unsupported policy provider: {provider}");
        return new PolicySnapshot(string.Empty, string.Empty);
    }

    private static PolicySnapshot ReadPostgresPolicy(MigrationTarget target)
    {
        using var command = target.Connection.CreateCommand();
        command.CommandText = """
            SELECT policy.qual, policy.with_check, table_catalog.relrowsecurity, policy.cmd
            FROM pg_policies policy
            JOIN pg_namespace table_schema ON table_schema.nspname = policy.schemaname
            JOIN pg_class table_catalog ON table_catalog.relnamespace = table_schema.oid
                                       AND table_catalog.relname = policy.tablename
            WHERE policy.schemaname = @schema AND policy.tablename = 'documents'
              AND policy.policyname = 'documents_access'
            """;
        AddParameter(command, "@schema", target.Schema);
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read(), "Missing documents_access policy in pg_policies");
        Assert.True(reader.GetBoolean(2), "Row-level security is disabled on documents");
        Assert.Equal("ALL", reader.GetString(3));
        var snapshot = new PolicySnapshot(reader.GetString(0), reader.GetString(1));
        Assert.False(reader.Read(), "Duplicate documents_access policy");
        return snapshot;
    }

    private static PolicySnapshot ReadSqlitePolicy(MigrationTarget target) =>
        new(
            ReadSqliteObjectSql(target, "view", "name = 'documents_secure'"),
            ReadSqliteObjectSql(
                target,
                "trigger",
                "tbl_name = 'documents' AND name LIKE '%insert%'"
            )
        );

    private static string ReadSqliteObjectSql(MigrationTarget target, string type, string condition)
    {
        using var command = target.Connection.CreateCommand();
        command.CommandText =
            $"SELECT sql FROM sqlite_master WHERE type = '{type}' AND {condition} ORDER BY name";
        using var reader = command.ExecuteReader();
        var definitions = ImmutableArray.CreateBuilder<string>();
        while (reader.Read())
        {
            definitions.Add(reader.GetString(0));
        }
        return Assert.Single(definitions);
    }

    private static PolicySnapshot ReadSqlServerPolicy(MigrationTarget target)
    {
        using var command = target.Connection.CreateCommand();
        command.CommandText = """
            SELECT predicate.predicate_type_desc, predicate.predicate_definition,
                   module.definition, policy.is_enabled
            FROM sys.security_policies policy
            JOIN sys.schemas policy_schema ON policy_schema.schema_id = policy.schema_id
            JOIN sys.security_predicates predicate ON predicate.object_id = policy.object_id
            JOIN sys.tables target_table ON target_table.object_id = predicate.target_object_id
            JOIN sys.schemas target_schema ON target_schema.schema_id = target_table.schema_id
            CROSS JOIN sys.sql_modules module
            JOIN sys.objects function_object ON function_object.object_id = module.object_id
            JOIN sys.schemas function_schema ON function_schema.schema_id = function_object.schema_id
            WHERE policy_schema.name = @schema AND target_schema.name = @schema
              AND target_table.name = 'documents' AND policy.name = 'documents_access'
              AND (
                LEFT(predicate.predicate_definition,
                     LEN(QUOTENAME(function_schema.name) + '.' + QUOTENAME(function_object.name) + '('))
                  = QUOTENAME(function_schema.name) + '.' + QUOTENAME(function_object.name) + '('
                OR LEFT(predicate.predicate_definition,
                     LEN(function_schema.name + '.' + function_object.name + '('))
                  = function_schema.name + '.' + function_object.name + '('
              )
            ORDER BY predicate.predicate_type_desc, predicate.predicate_definition
            """;
        AddParameter(command, "@schema", target.Schema);
        using var reader = command.ExecuteReader();
        var filters = ImmutableArray.CreateBuilder<string>();
        var blocks = ImmutableArray.CreateBuilder<string>();
        while (reader.Read())
        {
            Assert.True(reader.GetBoolean(3), "Security policy is disabled");
            var definition = $"{reader.GetString(1)}\n{reader.GetString(2)}";
            if (reader.GetString(0) == "FILTER")
            {
                filters.Add(definition);
            }
            else if (reader.GetString(0) == "BLOCK")
            {
                blocks.Add(definition);
            }
        }
        Assert.NotEmpty(filters);
        Assert.NotEmpty(blocks);
        return new PolicySnapshot(string.Join("\n", filters), string.Join("\n", blocks));
    }

    private static void AssertChangedClause(
        string provider,
        PolicySnapshot original,
        PolicySnapshot changed,
        bool usingAllowed,
        bool checkAllowed
    )
    {
        if (usingAllowed)
        {
            Assert.Equal(original.Using, changed.Using);
        }
        else
        {
            Assert.NotEqual(original.Using, changed.Using);
        }
        if (checkAllowed)
        {
            Assert.Equal(original.WithCheck, changed.WithCheck);
        }
        else
        {
            Assert.NotEqual(original.WithCheck, changed.WithCheck);
        }
        Assert.False(string.IsNullOrWhiteSpace(changed.Using), provider);
        Assert.False(string.IsNullOrWhiteSpace(changed.WithCheck), provider);
    }

    // Implements [RLS-DIFF]. Assert the requested clauses, including the one not changed.
    private static void AssertPolicyMatches(
        string provider,
        PolicySnapshot snapshot,
        bool usingAllowed,
        bool checkAllowed
    )
    {
        AssertPredicateMatches(provider, "USING", snapshot.Using, usingAllowed);
        AssertPredicateMatches(provider, "WITH CHECK", snapshot.WithCheck, checkAllowed);
    }

    private static void AssertPredicateMatches(
        string provider,
        string clause,
        string definition,
        bool allowed
    )
    {
        var expected = allowed ? "IS NOT NULL" : "IS NULL";
        var forbidden = allowed ? "IS NULL" : "IS NOT NULL";
        Assert.Contains(expected, definition, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(forbidden, definition, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id", definition, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(definition), $"{provider} {clause} is missing");
    }

    private sealed record PolicySnapshot(string Using, string WithCheck);
}
