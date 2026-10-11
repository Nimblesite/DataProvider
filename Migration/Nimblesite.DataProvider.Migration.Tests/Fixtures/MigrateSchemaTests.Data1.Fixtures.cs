// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class MigrateSchemaTests
{
    private const string Data_MigrateSchema_PartiallyMigrated_CreatesRemainingTables_1 = """
            CREATE TABLE IF NOT EXISTS "public"."countries" (
                "id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                "name" VARCHAR(100) NOT NULL,
                "code" VARCHAR(10) NOT NULL,
                CONSTRAINT "uq_countries_code" UNIQUE ("code")
            )
            """;

    private const string Data_MigrateSchema_PartiallyMigrated_CreatesRemainingTables_2 = """
            CREATE TABLE IF NOT EXISTS "public"."regions" (
                "id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                "name" VARCHAR(100) NOT NULL,
                "country_id" UUID NOT NULL,
                CONSTRAINT "fk_regions_country" FOREIGN KEY ("country_id")
                    REFERENCES "public"."countries" ("id") ON DELETE CASCADE
            )
            """;

    private const string Data_MigrateSchema_TableCreationFails_ContinuesWithOtherTables_1 = """
            CREATE TABLE IF NOT EXISTS "public"."orphan_suburbs" (
                "id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                "name" VARCHAR(100) NOT NULL
            )
            """;

    private const string Data_TableExists_1 = """
            SELECT EXISTS (
                SELECT FROM information_schema.tables
                WHERE table_schema = 'public'
                AND table_name = @tableName
            )
            """;

    private const string Data_IndexExists_1 = """
            SELECT EXISTS (
                SELECT FROM pg_indexes
                WHERE schemaname = 'public'
                AND indexname = @indexName
            )
            """;
}
