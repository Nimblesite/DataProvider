// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SchemaYamlSerializerTests
{
    private const string Data_FromYaml_ValidYaml_DeserializesCorrectly_1 = """
            name: test_schema
            tables:
              - name: Products
                schema: public
                columns:
                  - name: Id
                    type: Uuid
                    isNullable: false
                  - name: Name
                    type: VarChar(200)
                    isNullable: false
                  - name: Price
                    type: Decimal(10,2)
                    isNullable: true
                primaryKey:
                  columns:
                    - Id
            """;

    private const string Data_FromYamlFile_ValidFile_LoadsSchema_1 = """
            name: file_test
            tables:
              - name: TestTable
                columns:
                  - name: Id
                    type: Int
                    isNullable: false
            """;

    private const string Data_IntegrationTest_YamlToSqlite_CreatesDatabaseSuccessfully_1 = """
            name: integration_test
            tables:
              - name: Users
                schema: public
                columns:
                  - name: Id
                    type: Uuid
                    isNullable: false
                  - name: Email
                    type: VarChar(255)
                    isNullable: false
                  - name: Name
                    type: Text
                    isNullable: true
                primaryKey:
                  columns:
                    - Id
                indexes:
                  - name: idx_users_email
                    columns:
                      - Email
                    isUnique: true
              - name: Orders
                schema: public
                columns:
                  - name: Id
                    type: BigInt
                    isNullable: false
                    isIdentity: true
                  - name: UserId
                    type: Uuid
                    isNullable: false
                  - name: Total
                    type: Decimal(12,2)
                    isNullable: false
                primaryKey:
                  columns:
                    - Id
                foreignKeys:
                  - columns:
                      - UserId
                    referencedTable: Users
                    referencedSchema: public
                    referencedColumns:
                      - Id
                    onDelete: Cascade
                    onUpdate: NoAction
            """;
}
