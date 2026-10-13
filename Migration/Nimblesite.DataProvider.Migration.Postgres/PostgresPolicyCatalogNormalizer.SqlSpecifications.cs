// Implements [CI-INDEPENDENT-SQL-SPECIFICATIONS].
// Immutable SQL text only. Execution, binding and projection remain in the analysed source.
namespace Nimblesite.DataProvider.Migration.Postgres;

public static partial class PostgresPolicyCatalogNormalizer
{
    internal const string SqlSpecification1 = """
        SELECT definition FROM pg_views
        WHERE schemaname = pg_my_temp_schema()::regnamespace::text AND viewname = @view
        """;
    internal const string SqlSpecification2 = """
        SELECT table_name 
        FROM information_schema.tables 
        WHERE table_schema = @schema 
        AND table_type = 'BASE TABLE'
        ORDER BY table_name
        """;
}
