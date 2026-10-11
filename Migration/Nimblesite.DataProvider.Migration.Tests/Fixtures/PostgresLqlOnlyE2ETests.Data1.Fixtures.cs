// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresLqlOnlyE2ETests
{
    private const string Data_BootstrapRolesAndGucReaders_1 = """
            DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='lql_user') THEN
                    CREATE ROLE lql_user NOLOGIN NOBYPASSRLS;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='lql_admin') THEN
                    CREATE ROLE lql_admin NOLOGIN NOBYPASSRLS;
                END IF;
            END$$;

            CREATE OR REPLACE FUNCTION app_user_id() RETURNS uuid AS $$
                SELECT NULLIF(current_setting('rls.user_id', true), '')::uuid
            $$ LANGUAGE sql STABLE;

            CREATE OR REPLACE FUNCTION app_tenant_id() RETURNS uuid AS $$
                SELECT NULLIF(current_setting('rls.tenant_id', true), '')::uuid
            $$ LANGUAGE sql STABLE;
            """;

    private const string Data_BootstrapMembershipFns_1 = """
            CREATE OR REPLACE FUNCTION is_member(u uuid, t uuid) RETURNS bool
                LANGUAGE sql SECURITY DEFINER STABLE AS $$
                    SELECT EXISTS (
                        SELECT 1 FROM tenant_members WHERE user_id = u AND tenant_id = t
                    )
                $$;
            GRANT EXECUTE ON FUNCTION is_member(uuid, uuid) TO lql_user, lql_admin;
            """;

    private const string Data_LqlOnly_NapShape_AppliesAndCreatesPolicies_1 = """
            SELECT policyname, permissive, cmd FROM pg_policies
            WHERE schemaname='public' AND tablename='agent_configs'
            ORDER BY policyname
            """;
}
