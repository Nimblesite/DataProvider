// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresRlsE2ETests
{
    private static string SqlData_ApplyAndForceRls_1() =>
        $"""
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '{AppRole}') THEN
                    CREATE ROLE {AppRole} NOLOGIN NOBYPASSRLS;
                END IF;
            END$$;
            GRANT USAGE ON SCHEMA public TO {AppRole};
            """;
}
