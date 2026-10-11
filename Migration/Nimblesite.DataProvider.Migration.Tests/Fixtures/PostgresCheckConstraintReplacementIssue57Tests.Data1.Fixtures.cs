// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresCheckConstraintReplacementIssue57Tests
{
    private const string Data_LiveExpressionContains_1 = """
            SELECT pg_get_constraintdef(c.oid)
            FROM pg_constraint c
            JOIN pg_class t ON t.oid = c.conrelid
            JOIN pg_namespace n ON n.oid = t.relnamespace
            WHERE n.nspname = 'public'
              AND t.relname = 'usage_events'
              AND c.contype = 'c'
              AND c.conname = 'usage_events_kind_check'
            """;
}
