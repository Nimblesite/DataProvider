// Implements [MIG-CATALOG-INDEPENDENT-QUERY-DATA].
// Independent catalog query specifications. Execution and row projection stay in the inspector.
namespace Nimblesite.DataProvider.Migration.Postgres;

internal static partial class PostgresSupportSchemaInspector
{
    private const string CatalogQuery_InspectSchemaGrants_1 = """
        SELECT grantee.rolname, acl.privilege_type
        FROM pg_namespace n
        CROSS JOIN LATERAL aclexplode(COALESCE(n.nspacl, acldefault('n', n.nspowner))) acl
        JOIN pg_roles grantee ON grantee.oid = acl.grantee
        WHERE n.nspname = @schema
        ORDER BY grantee.rolname, acl.privilege_type
        """;
    private const string CatalogQuery_InspectTableGrants_2 = """
        SELECT c.relname, grantee.rolname, acl.privilege_type
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        CROSS JOIN LATERAL aclexplode(COALESCE(c.relacl, acldefault('r', c.relowner))) acl
        JOIN pg_roles grantee ON grantee.oid = acl.grantee
        WHERE n.nspname = @schema
          AND c.relkind IN ('r', 'p')
        ORDER BY c.relname, grantee.rolname, acl.privilege_type
        """;
}
