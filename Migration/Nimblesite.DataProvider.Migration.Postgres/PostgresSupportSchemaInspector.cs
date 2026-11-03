using System.Collections.ObjectModel;

namespace Nimblesite.DataProvider.Migration.Postgres;

internal static partial class PostgresSupportSchemaInspector
{
    public static IReadOnlyList<PostgresRoleDefinition> InspectRoles(NpgsqlConnection connection)
    {
        var roles = new List<PostgresRoleDefinition>();
        PostgresCatalogQuery.Read(
            connection: connection,
            configure: command =>
            {
                command.CommandText = """
                SELECT
                    r.rolname,
                    r.rolcanlogin,
                    r.rolbypassrls,
                    COALESCE(
                        array_agg(m.rolname ORDER BY m.rolname)
                            FILTER (WHERE m.rolname IS NOT NULL),
                        ARRAY[]::text[]
                    ) AS grant_to
                FROM pg_roles r
                LEFT JOIN pg_auth_members am ON am.roleid = r.oid
                LEFT JOIN pg_roles m ON m.oid = am.member
                WHERE r.rolname NOT LIKE 'pg\_%' ESCAPE '\'
                GROUP BY r.rolname, r.rolcanlogin, r.rolbypassrls
                ORDER BY r.rolname
                """;
            },
            read: reader =>
            {
                roles.Add(
                    new PostgresRoleDefinition
                    {
                        Name = reader.GetString(0),
                        Login = reader.GetBoolean(1),
                        BypassRls = reader.GetBoolean(2),
                        GrantTo = reader.GetValue(3) as string[] ?? [],
                    }
                );
            }
        );
        return roles.AsReadOnly();
    }

    public static IReadOnlyList<PostgresFunctionDefinition> InspectFunctions(
        NpgsqlConnection connection,
        string schemaName
    )
    {
        var functions = new List<PostgresFunctionDefinition>();
        PostgresCatalogQuery.Read(
            connection: connection,
            configure: command =>
            {
                command.CommandText = """
                SELECT
                    n.nspname,
                    p.proname,
                    COALESCE(p.proargnames, ARRAY[]::text[]) AS arg_names,
                    ARRAY(
                        SELECT format_type(arg_type, NULL)
                        FROM unnest(p.proargtypes) WITH ORDINALITY AS a(arg_type, ord)
                        ORDER BY ord
                    ) AS arg_types,
                    pg_get_function_result(p.oid) AS returns,
                    l.lanname,
                    CASE p.provolatile
                        WHEN 'i' THEN 'immutable'
                        WHEN 's' THEN 'stable'
                        ELSE 'volatile'
                    END AS volatility,
                    p.prosecdef,
                    p.prosrc,
                    owner.rolname AS owner_role,
                    (
                        SELECT config.entry
                        FROM pg_proc pc,
                            unnest(pc.proconfig) AS config(entry)
                        WHERE pc.oid = p.oid AND config.entry LIKE 'search_path=%'
                        LIMIT 1
                    ) AS search_path_config,
                    NOT EXISTS (
                        SELECT 1
                        FROM aclexplode(COALESCE(p.proacl, acldefault('f', p.proowner))) acl
                        WHERE acl.grantee = 0 AND acl.privilege_type = 'EXECUTE'
                    ) AS revoke_public_execute,
                    COALESCE(
                        ARRAY(
                            SELECT grantee.rolname
                            FROM aclexplode(COALESCE(p.proacl, acldefault('f', p.proowner))) acl
                            JOIN pg_roles grantee ON grantee.oid = acl.grantee
                            WHERE acl.privilege_type = 'EXECUTE'
                              AND acl.grantee <> p.proowner
                            ORDER BY grantee.rolname
                        ),
                        ARRAY[]::text[]
                    ) AS execute_roles
                FROM pg_proc p
                JOIN pg_namespace n ON n.oid = p.pronamespace
                JOIN pg_language l ON l.oid = p.prolang
                JOIN pg_roles owner ON owner.oid = p.proowner
                WHERE n.nspname = @schema
                  -- [MIG-TRIGGER-PG]: trigger guard functions are owned by the
                  -- trigger lifecycle; reading them back would make destructive
                  -- runs try to drop them while their trigger still depends on them.
                  AND p.proname NOT LIKE @guardFunctionPattern ESCAPE '\'
                ORDER BY n.nspname, p.proname, p.oid
                """;
                command.Parameters.AddWithValue("@schema", schemaName);
                command.Parameters.AddWithValue(
                    "@guardFunctionPattern",
                    $"%{TriggerDdlSupport.GuardFunctionSuffix.Replace("_", "\\_", StringComparison.Ordinal)}"
                );
            },
            read: reader =>
            {
                var argNames = reader.GetValue(2) as string[] ?? [];
                var argTypes = reader.GetValue(3) as string[] ?? [];
                functions.Add(
                    new PostgresFunctionDefinition
                    {
                        Schema = reader.GetString(0),
                        Name = reader.GetString(1),
                        Arguments = ToArguments(argNames, argTypes),
                        Returns = reader.GetString(4),
                        Language = reader.GetString(5),
                        Volatility = reader.GetString(6),
                        SecurityDefiner = reader.GetBoolean(7),
                        Body = reader.GetString(8),
                        OwnerRole = reader.GetString(10),
                        SearchPath = ParseSearchPathConfig(reader.IsDBNull(11) ? null : reader.GetString(11)),
                        RevokePublicExecute = reader.GetBoolean(9),
                        ExecuteRoles = reader.GetValue(12) as string[] ?? [],                    }
                );
            }
        );
        return functions.AsReadOnly();
    }

    public static IReadOnlyList<PostgresGrantDefinition> InspectGrants(
        NpgsqlConnection connection,
        string schemaName
    )
    {
        var grants = new List<PostgresGrantDefinition>();
        grants.AddRange(
            InspectGrants(
                connection: connection,
                schemaName: schemaName,
                catalogQuery: CatalogQuery_InspectSchemaGrants_1,
                target: PostgresGrantTarget.Schema
            )
        );
        grants.AddRange(
            InspectGrants(
                connection: connection,
                schemaName: schemaName,
                catalogQuery: CatalogQuery_InspectTableGrants_2,
                target: PostgresGrantTarget.Table
            )
        );
        return grants.AsReadOnly();
    }

    // Implements [RLS-PG-FUNCTION-SECURITY]: proconfig stores GUC entries such as
    // `search_path=pg_catalog, "app"`; entries are unquoted with the same rules
    // QuoteIdent applies. Absent config keeps the function's existing schemas.
    internal static IReadOnlyList<string> ParseSearchPathConfig(string? config) =>
        config is null || !config.StartsWith("search_path=", StringComparison.Ordinal)
            ? []
            : config["search_path=".Length..]
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(Unquote)
                .ToList();

    private static string Unquote(string entry) =>
        entry.Length >= 2 && entry.StartsWith('"') && entry.EndsWith('"')
            ? entry[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal)
            : entry;

    private static ReadOnlyCollection<PostgresFunctionArgumentDefinition> ToArguments(
        string[] argNames,
        string[] argTypes
    ) =>
        argTypes
            .Select(
                (argType, index) =>
                    new PostgresFunctionArgumentDefinition
                    {
                        Name = index < argNames.Length ? argNames[index] : string.Empty,
                        Type = argType,
                    }
            )
            .ToList()
            .AsReadOnly();

    private static ReadOnlyCollection<PostgresGrantDefinition> InspectGrants(
        NpgsqlConnection connection,
        string schemaName,
        string catalogQuery,
        PostgresGrantTarget target
    )
    {
        using var command = connection.CreateCommand();
        command.CommandText = catalogQuery;
        command.Parameters.AddWithValue("@schema", schemaName);
        return ReadGrantRows(command, schemaName, target, null);
    }

    private static ReadOnlyCollection<PostgresGrantDefinition> ReadGrantRows(
        NpgsqlCommand command,
        string schemaName,
        PostgresGrantTarget target,
        string? objectName
    )
    {
        using var reader = command.ExecuteReader();
        var rows = new List<PostgresGrantRow>();
        while (reader.Read())
        {
            if (target == PostgresGrantTarget.Table)
            {
                rows.Add(
                    new PostgresGrantRow(
                        reader.GetString(0),
                        reader.GetString(1),
                        reader.GetString(2)
                    )
                );
            }
            else
            {
                rows.Add(
                    new PostgresGrantRow(objectName, reader.GetString(0), reader.GetString(1))
                );
            }
        }
        return ToGrantDefinitions(schemaName, target, rows);
    }

    private static ReadOnlyCollection<PostgresGrantDefinition> ToGrantDefinitions(
        string schemaName,
        PostgresGrantTarget target,
        IReadOnlyList<PostgresGrantRow> rows
    ) =>
        rows.GroupBy(r => new { r.ObjectName, r.Role })
            .Select(g => new PostgresGrantDefinition
            {
                Schema = schemaName,
                Target = target,
                ObjectName = g.Key.ObjectName,
                Roles = [g.Key.Role],
                Privileges = g.Select(r => r.Privilege).Distinct().OrderBy(p => p).ToList(),
            })
            .ToList()
            .AsReadOnly();

    private sealed record PostgresGrantRow(string? ObjectName, string Role, string Privilege);
}
