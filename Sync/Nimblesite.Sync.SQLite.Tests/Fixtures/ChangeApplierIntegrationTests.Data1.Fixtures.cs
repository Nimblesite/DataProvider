// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.Sync.SQLite.Tests;

public partial class ChangeApplierIntegrationTests
{
    private const string Data_Shared_1 = """
                CREATE TABLE Person (
                    Id TEXT PRIMARY KEY,
                    Name TEXT NOT NULL,
                    Age INTEGER,
                    Email TEXT
                );
                """;

    private const string Data_ApplyChange_Insert_WithForeignKeyViolation_ReturnsFalse_1 = """
                PRAGMA foreign_keys = ON;
                CREATE TABLE Department (Id TEXT PRIMARY KEY, Name TEXT);
                CREATE TABLE Employee (
                    Id TEXT PRIMARY KEY,
                    Name TEXT,
                    DeptId TEXT REFERENCES Department(Id)
                );
                """;

    private const string Data_ApplyChange_Insert_WithValidForeignKey_Succeeds_1 = """
                PRAGMA foreign_keys = ON;
                CREATE TABLE Department (Id TEXT PRIMARY KEY, Name TEXT);
                INSERT INTO Department (Id, Name) VALUES ('d1', 'Engineering');
                CREATE TABLE Employee (
                    Id TEXT PRIMARY KEY,
                    Name TEXT,
                    DeptId TEXT REFERENCES Department(Id)
                );
                """;
}
