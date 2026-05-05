using System.Collections.ObjectModel;

namespace Nimblesite.DataProvider.Migration.Postgres;

internal static class PostgresSupportSchemaInspector
{
    public static IReadOnlyList<PostgresRoleDefinition> InspectRoles(NpgsqlConnection connection)
    {
        using var command = connection.CreateCommand();
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

        using var reader = command.ExecuteReader();
        var roles = new List<PostgresRoleDefinition>();
        while (reader.Read())
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
        return roles.AsReadOnly();
    }

    public static IReadOnlyList<PostgresFunctionDefinition> InspectFunctions(
        NpgsqlConnection connection,
        string schemaName
    )
    {
        using var command = connection.CreateCommand();
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
            WHERE n.nspname = @schema
            ORDER BY n.nspname, p.proname, p.oid
            """;
        command.Parameters.AddWithValue("@schema", schemaName);

        using var reader = command.ExecuteReader();
        var functions = new List<PostgresFunctionDefinition>();
        while (reader.Read())
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
                    RevokePublicExecute = reader.GetBoolean(9),
                    ExecuteRoles = reader.GetValue(10) as string[] ?? [],
                }
            );
        }
        return functions.AsReadOnly();
    }

    public static IReadOnlyList<PostgresGrantDefinition> InspectGrants(
        NpgsqlConnection connection,
        string schemaName
    )
    {
        var grants = new List<PostgresGrantDefinition>();
        grants.AddRange(InspectSchemaGrants(connection, schemaName));
        grants.AddRange(InspectTableGrants(connection, schemaName));
        return grants.AsReadOnly();
    }

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

    private static ReadOnlyCollection<PostgresGrantDefinition> InspectSchemaGrants(
        NpgsqlConnection connection,
        string schemaName
    )
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT grantee.rolname, acl.privilege_type
            FROM pg_namespace n
            CROSS JOIN LATERAL aclexplode(COALESCE(n.nspacl, acldefault('n', n.nspowner))) acl
            JOIN pg_roles grantee ON grantee.oid = acl.grantee
            WHERE n.nspname = @schema
            ORDER BY grantee.rolname, acl.privilege_type
            """;
        command.Parameters.AddWithValue("@schema", schemaName);
        return ReadGrantRows(command, schemaName, PostgresGrantTarget.Schema, null);
    }

    private static ReadOnlyCollection<PostgresGrantDefinition> InspectTableGrants(
        NpgsqlConnection connection,
        string schemaName
    )
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.relname, grantee.rolname, acl.privilege_type
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            CROSS JOIN LATERAL aclexplode(COALESCE(c.relacl, acldefault('r', c.relowner))) acl
            JOIN pg_roles grantee ON grantee.oid = acl.grantee
            WHERE n.nspname = @schema
              AND c.relkind IN ('r', 'p')
            ORDER BY c.relname, grantee.rolname, acl.privilege_type
            """;
        command.Parameters.AddWithValue("@schema", schemaName);
        return ReadGrantRows(command, schemaName, PostgresGrantTarget.Table, null);
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
