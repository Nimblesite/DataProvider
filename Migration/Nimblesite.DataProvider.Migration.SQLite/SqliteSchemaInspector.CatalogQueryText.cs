// Implements [MIG-CATALOG-INDEPENDENT-QUERY-DATA].
// Independent catalog query specifications. Execution and row projection stay in the inspector.
namespace Nimblesite.DataProvider.Migration.SQLite;

public static partial class SqliteSchemaInspector
{
    private const string CatalogQuery_Inspect_1 = """
        SELECT name FROM sqlite_master 
        WHERE type = 'table' 
        AND name NOT LIKE 'sqlite_%'
        AND name <> '__rls_context'
        ORDER BY name
        """;
}
