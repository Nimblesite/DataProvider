// Implements [CI-INDEPENDENT-SQL-SPECIFICATIONS].
// Immutable SQL text only. Execution, binding and projection remain in the analysed source.
namespace Nimblesite.DataProvider.Migration.SQLite;

public static partial class SqliteSchemaInspector
{
    internal const string SqlSpecification1 = """
        SELECT sql FROM sqlite_master
        WHERE type = 'index' AND name = @name
        """;
    internal const string SqlSpecification2 = """
        SELECT name, sql FROM sqlite_master
        WHERE type IN ('index', 'trigger') AND tbl_name = $table AND sql IS NOT NULL
        ORDER BY type, name
        """;
    internal const string SqlSpecification3 = """
        SELECT name FROM sqlite_master
        WHERE type = 'trigger' AND tbl_name = @table AND name LIKE @pattern
        ORDER BY name
        """;
    internal const string SqlSpecification4 = """
        SELECT name, sql FROM sqlite_master
        WHERE type = 'trigger' AND tbl_name = @table AND name LIKE @pattern
        ORDER BY name
        """;
}
