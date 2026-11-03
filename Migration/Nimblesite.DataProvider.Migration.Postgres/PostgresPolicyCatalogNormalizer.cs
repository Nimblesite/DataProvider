using SqlParser.Dialects;
using PredicateOk = Outcome.Result<
    string,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Ok<string, Nimblesite.DataProvider.Migration.Core.MigrationError>;
using SchemaOk = Outcome.Result<
    Nimblesite.DataProvider.Migration.Core.SchemaDefinition,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Ok<
    Nimblesite.DataProvider.Migration.Core.SchemaDefinition,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>;

namespace Nimblesite.DataProvider.Migration.Postgres;

/// <summary>
/// Uses PostgreSQL's own parser to compare desired RLS predicates with catalog predicates.
/// Temporary views are dropped in the same session and never alter application schema.
/// Implements [RLS-DIFF].
/// </summary>
public static partial class PostgresPolicyCatalogNormalizer
{
    /// <summary>
    /// Inspect the live schema with policy predicates in the same view context
    /// used for desired predicates. Implements [RLS-DIFF] and issue #119.
    /// PostgreSQL 15 qualifies view columns differently from pg_policies;
    /// normalizing both preserves correlated references and subquery shadowing.
    /// Implements [MIG-POSTGRES-CUSTOM-SCHEMA]: the inspected schema is the
    /// caller's choice; "public" remains the default for compatibility.
    /// </summary>
    public static SchemaResult Inspect(NpgsqlConnection connection, string schemaName = "public")
    {
        var result = PostgresSchemaInspector.Inspect(
            connection: connection,
            schemaName: schemaName
        );
        return result is SchemaOk ok
            ? Normalize(connection: connection, live: ok.Value, desired: ok.Value)
            : result;
    }

    /// <summary>
    /// Canonicalize policies and catalog-equivalent expressions on tables that
    /// already exist. Implements [MIG-POSTGRES-CATALOG-EQUIVALENT-EXPRESSIONS]:
    /// PostgreSQL deparses check-constraint and partial-index predicates
    /// differently from the YAML text (literal ::text casts, dropped redundant
    /// parentheses, <c>INTERVAL '24 hours'</c> as <c>'24:00:00'::interval</c>).
    /// Both the live and desired forms pass through PostgreSQL's own parser
    /// via a temporary view, so comparison is semantic, never textual — and a
    /// genuinely different predicate still compares different.
    /// </summary>
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
        var normalized = table with
        {
            Columns = NormalizeColumns(connection, table),
            CheckConstraints = NormalizeChecks(connection, table),
            Indexes = NormalizeIndexes(connection, table),
        };
        return normalized.RowLevelSecurity is not { } rls
            ? normalized
            : normalized with
            {
                RowLevelSecurity = rls with
                {
                    Policies = rls
                        .Policies.Select(p => NormalizePolicy(connection, table, p))
                        .ToArray()
                }
            };
    }

    private static ColumnDefinition[] NormalizeColumns(
        NpgsqlConnection connection,
        TableDefinition table
    ) =>
        table.Columns
            .Select(c =>
                c.CheckConstraint is null
                    ? c
                    : c with { CheckConstraint = Canonicalize(connection, table, c.CheckConstraint) }
            )
            .ToArray();

    private static CheckConstraintDefinition[] NormalizeChecks(
        NpgsqlConnection connection,
        TableDefinition table
    ) =>
        table.CheckConstraints
            .Select(cc => cc with
            {
                Expression = Canonicalize(connection, table, cc.Expression)
            })
            .ToArray();

    private static IndexDefinition[] NormalizeIndexes(
        NpgsqlConnection connection,
        TableDefinition table
    ) =>
        table.Indexes
            .Select(i =>
                i.Filter is null ? i : i with { Filter = Canonicalize(connection, table, i.Filter) }
            )
            .ToArray();

    private static string Canonicalize(
        NpgsqlConnection connection,
        TableDefinition table,
        string expression
    ) =>
        CanonicalizeClause(connection, table, expression, lql: null, name: table.Name) ?? expression;

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
        command.CommandText = global::Nimblesite
            .DataProvider
            .Migration
            .Postgres
            .PostgresPolicyCatalogNormalizer
            .SqlSpecification1;
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
        var selection = SqlSelectionExtractor.ParseSingleSelectSelection(
            definition,
            new PostgreSqlDialect()
        );
        // Implements [MIG-POSTGRES-CATALOG-EQUIVALENT-EXPRESSIONS]: render the
        // parsed predicate in the canonical form instead of raw ToSql(), which
        // prints operator tokens like Plus instead of +.
        return selection is null ? null : PostgresCheckExpressionNormalizer.RenderCanonical(selection);
    }
}
