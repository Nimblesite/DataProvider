// Implements [MIG-CATALOG-INDEPENDENT-QUERY-DATA].
// Independent catalog query specifications. Execution and row projection stay in the inspector.
namespace Nimblesite.DataProvider.Migration.Postgres;

internal static partial class PostgresTriggerSchemaInspector
{
    private const string CatalogQuery_Inspect_1 = """
        SELECT trigger_name, event_manipulation, action_timing, action_orientation
        FROM information_schema.triggers
        WHERE event_object_schema = @schema AND event_object_table = @table
          AND trigger_name LIKE @managedPattern ESCAPE '\'
        ORDER BY trigger_name
        """;
}
