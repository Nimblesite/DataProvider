// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresRlsNapShapeTests
{
    private const string Data_BootstrapNapPrelude_1 = """
        DO $$ BEGIN
            IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='nap_app_user') THEN
                CREATE ROLE nap_app_user NOLOGIN NOBYPASSRLS;
            END IF;
            IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='nap_app_admin') THEN
                CREATE ROLE nap_app_admin NOLOGIN NOBYPASSRLS;
            END IF;
        END$$;

        CREATE OR REPLACE FUNCTION app_user_id() RETURNS uuid AS $$
            SELECT NULLIF(current_setting('rls.user_id', true), '')::uuid
        $$ LANGUAGE sql STABLE;

        CREATE OR REPLACE FUNCTION app_tenant_id() RETURNS uuid AS $$
            SELECT NULLIF(current_setting('rls.tenant_id', true), '')::uuid
        $$ LANGUAGE sql STABLE;
        """;

    private const string Data_NapShape_FourTenantTables_AppliesCleanlyAndIsForced_1 = """
        SELECT relname, relrowsecurity, relforcerowsecurity
        FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname='public' AND relname IN ('agent_configs','conversations','messages','api_keys')
        ORDER BY relname
        """;
}
