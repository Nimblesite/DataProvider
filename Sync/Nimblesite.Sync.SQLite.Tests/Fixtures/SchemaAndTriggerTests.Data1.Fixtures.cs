// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.Sync.SQLite.Tests;

public partial class SchemaAndTriggerTests
{
    private const string Data_CreateSchema_EmptyDatabase_CreatesAllTables_1 = """
            SELECT name FROM sqlite_master
            WHERE type='table'
            AND name LIKE '_sync%'
            ORDER BY name
            """;

    private const string Data_CreateSchema_CreatesSyncClientsIndex_1 = """
            SELECT name FROM sqlite_master
            WHERE type='index'
            AND name LIKE 'idx_sync_clients%'
            """;

    private const string Data_GenerateTriggersFromSchema_ValidTable_ReturnsTriggers_1 = """
                CREATE TABLE Products (
                    ProductId TEXT PRIMARY KEY,
                    Name TEXT NOT NULL,
                    Price REAL
                );
                """;

    private const string Data_CreateTriggers_ValidTable_CreatesTriggers_1 = """
            CREATE TABLE Orders (
                OrderId TEXT PRIMARY KEY,
                CustomerId TEXT,
                Total REAL
            );
            """;

    private const string Data_CreateTriggers_ValidTable_CreatesTriggers_2 = """
            SELECT name FROM sqlite_master
            WHERE type='trigger'
            AND name LIKE 'Orders_sync%'
            ORDER BY name
            """;

    private const string Data_DropTriggers_ExistingTriggers_RemovesThem_1 = """
            CREATE TABLE Items (
                ItemId TEXT PRIMARY KEY,
                Description TEXT
            );
            """;

    private const string Data_GetTableColumns_ValidTable_ReturnsColumns_1 = """
            CREATE TABLE Customers (
                CustomerId TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Email TEXT,
                Age INTEGER
            );
            """;

    private const string Data_Triggers_InsertLogsToSyncLog_1 = """
            CREATE TABLE Events (
                EventId TEXT PRIMARY KEY,
                Title TEXT
            );
            """;

    private const string Data_Triggers_UpdateLogsToSyncLog_1 = """
            CREATE TABLE Events (
                EventId TEXT PRIMARY KEY,
                Title TEXT
            );
            """;

    private const string Data_Triggers_DeleteLogsToSyncLog_1 = """
            CREATE TABLE Events (
                EventId TEXT PRIMARY KEY,
                Title TEXT
            );
            """;

    private const string Data_Triggers_SyncActiveSuppressions_1 = """
            CREATE TABLE Events (
                EventId TEXT PRIMARY KEY,
                Title TEXT
            );
            """;

    private const string Data_Triggers_LogPrimaryKeyAsJson_1 = """
            CREATE TABLE Records (
                RecordId TEXT PRIMARY KEY,
                Data TEXT
            );
            """;

    private const string Data_Triggers_LogPayloadAsJson_1 = """
            CREATE TABLE Records (
                RecordId TEXT PRIMARY KEY,
                Name TEXT,
                Value INTEGER
            );
            """;

    private const string Data_Triggers_LogOriginId_1 = """
            CREATE TABLE Tasks (
                TaskId TEXT PRIMARY KEY,
                Title TEXT
            );
            """;

    private const string Data_Triggers_LogTimestamp_1 = """
            CREATE TABLE Tasks (
                TaskId TEXT PRIMARY KEY,
                Title TEXT
            );
            """;

    private const string Data_GenerateTriggersFromSchema_TableWithoutPK_ReturnsError_1 = """
                CREATE TABLE NoPrimaryKey (
                    Name TEXT,
                    Value INTEGER
                );
                """;
}
