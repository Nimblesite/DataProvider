// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresDropConstraintBackedIndexTests
{
    private const string Data_ConstraintExists_1 = """
            SELECT EXISTS (
                SELECT 1
                FROM pg_constraint c
                JOIN pg_class t ON t.oid = c.conrelid
                JOIN pg_namespace n ON n.oid = t.relnamespace
                WHERE n.nspname = @schema
                AND t.relname = @table
                AND c.conname = @constraint
            )
            """;

    private const string Data_IndexExists_1 = """
            SELECT EXISTS (
                SELECT 1
                FROM pg_class i
                JOIN pg_namespace n ON n.oid = i.relnamespace
                WHERE n.nspname = @schema
                AND i.relname = @constraint
                AND i.relkind = 'i'
            )
            """;
}
