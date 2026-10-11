// Implements [CI-INDEPENDENT-SQL-SPECIFICATIONS].
// Immutable SQL text only. Execution, binding and projection remain in the analysed source.
namespace DataProvider;

internal static partial class PostgresCli
{
    internal const string SqlSpecification1 = """
        SELECT c.column_name, c.data_type, c.is_nullable, c.column_default,
               COALESCE(c.is_identity, 'NO') as is_identity,
               CASE WHEN pk.column_name IS NOT NULL THEN 'YES' ELSE 'NO' END as is_pk
        FROM information_schema.columns c
        LEFT JOIN (
            SELECT kcu.column_name
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
                ON tc.constraint_name = kcu.constraint_name
            WHERE tc.table_schema = @schema
                AND tc.table_name = @table
                AND tc.constraint_type = 'PRIMARY KEY'
        ) pk ON pk.column_name = c.column_name
        WHERE c.table_schema = @schema AND c.table_name = @table
        ORDER BY c.ordinal_position
        """;
}
