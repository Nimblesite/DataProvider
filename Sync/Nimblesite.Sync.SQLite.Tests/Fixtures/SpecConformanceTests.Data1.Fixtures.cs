// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.Sync.SQLite.Tests;

public partial class SpecConformanceTests
{
    private const string Data_Spec4_1_SingleColumnUuidPrimaryKey_IsSupported_1 = """
            CREATE TABLE UuidTable (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL
            );
            """;

    private const string Data_Spec17_1_SingleColumnPrimaryKey_Required_1 = """
            CREATE TABLE CompositePkTable (
                Key1 TEXT,
                Key2 TEXT,
                Value TEXT,
                PRIMARY KEY (Key1, Key2)
            );
            """;

    private const string Data_CreatePersonTable_1 = """
            CREATE TABLE Person (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Email TEXT NOT NULL
            );
            """;
}
