// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.Sync.Postgres.Tests;

public partial class PostgresRepositoryTests
{
    private const string Data_ApplyChange_Insert_CreatesRow_1 = """
                CREATE TABLE IF NOT EXISTS test_person (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL
                )
                """;

    private const string Data_ApplyChange_Update_UpdatesRow_1 = """
                CREATE TABLE IF NOT EXISTS test_update (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL
                );
                INSERT INTO test_update (id, name) VALUES ('u1', 'Original');
                """;

    private const string Data_ApplyChange_Delete_RemovesRow_1 = """
            CREATE TABLE IF NOT EXISTS test_delete (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL
            );
            INSERT INTO test_delete (id, name) VALUES ('d1', 'ToDelete');
            """;

    private const string Data_ApplyChange_Insert_Upserts_1 = """
                CREATE TABLE IF NOT EXISTS test_upsert (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL
                );
                INSERT INTO test_upsert (id, name) VALUES ('x1', 'Original');
                """;

    private const string Data_ApplyChange_Update_NonExistent_UpsertsViaInsert_1 = """
                CREATE TABLE IF NOT EXISTS test_update_upsert (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL
                )
                """;

    private const string Data_ApplyChange_ForeignKeyViolation_ReturnsFalse_1 = """
                CREATE TABLE IF NOT EXISTS test_parent (
                    id TEXT PRIMARY KEY
                );
                CREATE TABLE IF NOT EXISTS test_child (
                    id TEXT PRIMARY KEY,
                    parent_id TEXT REFERENCES test_parent(id)
                )
                """;

    private const string Data_CreateTriggers_CreatesAllTriggers_1 = """
            CREATE TABLE IF NOT EXISTS test_trigger_table (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL
            )
            """;

    private const string Data_Triggers_LogInsert_1 = """
                CREATE TABLE IF NOT EXISTS test_log_insert (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL
                )
                """;

    private const string Data_Triggers_LogUpdate_1 = """
                CREATE TABLE IF NOT EXISTS test_log_update (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL
                );
                INSERT INTO test_log_update (id, name) VALUES ('tu1', 'Before');
                """;

    private const string Data_Triggers_LogDelete_1 = """
                CREATE TABLE IF NOT EXISTS test_log_delete (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL
                );
                INSERT INTO test_log_delete (id, name) VALUES ('td1', 'ToDelete');
                """;

    private const string Data_Triggers_SuppressedWhenActive_1 = """
                CREATE TABLE IF NOT EXISTS test_suppression (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL
                )
                """;
}
