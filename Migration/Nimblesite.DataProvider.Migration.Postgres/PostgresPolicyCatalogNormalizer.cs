using SqlParser;
using SqlParser.Ast;
using SqlParser.Dialects;
using PredicateOk = Outcome.Result<
    string,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Ok<string, Nimblesite.DataProvider.Migration.Core.MigrationError>;

namespace Nimblesite.DataProvider.Migration.Postgres;

/// <summary>
/// Uses PostgreSQL's own parser to compare desired RLS predicates with catalog predicates.
/// Temporary views are dropped in the same session and never alter application schema.
/// Implements [RLS-DIFF].
/// </summary>
public static class PostgresPolicyCatalogNormalizer
{
    /// <summary>Canonicalize policies on tables that already exist.</summary>
    public static SchemaResult Normalize(
        NpgsqlConnection connection,
        SchemaDefinition live,
        SchemaDefinition desired
    )
    {
        try
        {
            var existing = live.Tables.Select(t => (t.Schema, t.Name)).ToHashSet();
            var tables = desired
                .Tables.Select(t =>
                    existing.Contains((t.Schema, t.Name)) ? NormalizeTable(connection, t) : t
                )
                .ToArray();
            return new SchemaResult.Ok<SchemaDefinition, MigrationError>(
                desired with
                {
                    Tables = tables,
                }
            );
        }
        catch (Exception ex)
        {
            return new SchemaResult.Error<SchemaDefinition, MigrationError>(
                MigrationError.FromException(ex)
            );
        }
    }

    private static TableDefinition NormalizeTable(
        NpgsqlConnection connection,
        TableDefinition table
    )
    {
        if (table.RowLevelSecurity is not { } rls)
        {
            return table;
        }
        var policies = rls.Policies.Select(p => NormalizePolicy(connection, table, p)).ToArray();
        return table with { RowLevelSecurity = rls with { Policies = policies } };
    }

    private static RlsPolicyDefinition NormalizePolicy(
        NpgsqlConnection connection,
        TableDefinition table,
        RlsPolicyDefinition policy
    )
    {
        var usingSql = CanonicalizeClause(
            connection,
            table,
            policy.UsingSql,
            policy.UsingLql,
            policy.Name
        );
        var checkSql = CanonicalizeClause(
            connection,
            table,
            policy.WithCheckSql,
            policy.WithCheckLql,
            policy.Name
        );
        return policy with
        {
            UsingSql = usingSql ?? policy.UsingSql,
            UsingLql = usingSql is null ? policy.UsingLql : null,
            WithCheckSql = checkSql ?? policy.WithCheckSql,
            WithCheckLql = checkSql is null ? policy.WithCheckLql : null,
        };
    }

    private static string? CanonicalizeClause(
        NpgsqlConnection connection,
        TableDefinition table,
        string? sql,
        string? lql,
        string name
    )
    {
        var expression = Resolve(sql, lql, name);
        if (expression is null)
        {
            return null;
        }
        try
        {
            return ReadCanonicalClause(connection, table, expression);
        }
        catch (PostgresException ex) when (ex.SqlState is "42703" or "42P01" or "42883" or "42704")
        {
            // A new column, table, or function can be unavailable before the
            // structural phase. The original expression remains fail-closed.
            return null;
        }
    }

    private static string? Resolve(string? sql, string? lql, string name)
    {
        if (!string.IsNullOrWhiteSpace(sql))
        {
            return sql;
        }
        if (string.IsNullOrWhiteSpace(lql))
        {
            return null;
        }
        var translated = RlsPredicateTranspiler.Translate(lql, RlsPlatform.Postgres, name);
        return translated is PredicateOk ok ? ok.Value : null;
    }

    private static string? ReadCanonicalClause(
        NpgsqlConnection connection,
        TableDefinition table,
        string expression
    )
    {
        var view = $"dp_rls_check_{Guid.NewGuid():N}";
        using var create = connection.CreateCommand();
        create.CommandText =
            $"CREATE TEMP VIEW {PostgresDdlGenerator.QuoteIdent(view)} AS SELECT 1 AS marker FROM {PostgresDdlGenerator.QuoteIdent(table.Schema)}.{PostgresDdlGenerator.QuoteIdent(table.Name)} WHERE ({expression})";
        create.ExecuteNonQuery();
        try
        {
            return ReadViewPredicate(connection, view);
        }
        finally
        {
            using var drop = connection.CreateCommand();
            drop.CommandText = $"DROP VIEW {PostgresDdlGenerator.QuoteIdent(view)}";
            drop.ExecuteNonQuery();
        }
    }

    private static string? ReadViewPredicate(NpgsqlConnection connection, string view)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT definition FROM pg_views
            WHERE schemaname = pg_my_temp_schema()::regnamespace::text AND viewname = @view
            """;
        command.Parameters.AddWithValue("view", view);
        if (command.ExecuteScalar() is not string definition)
        {
            throw new InvalidOperationException(
                $"Temporary policy view {view} was not found in pg_views"
            );
        }
        return ParseSelection(definition)
            ?? throw new InvalidOperationException(
                $"Could not parse temporary policy view: {definition}"
            );
    }

    private static string? ParseSelection(string definition)
    {
        try
        {
            var statements = new Parser().ParseSql(definition, new PostgreSqlDialect());
            return
                statements.Count == 1
                && statements[0] is Statement.Select select
                && select.Query.Body is SetExpression.SelectExpression body
                ? body.Select.Selection?.ToSql()
                : null;
        }
        catch (ParserException)
        {
            return null;
        }
    }
}
