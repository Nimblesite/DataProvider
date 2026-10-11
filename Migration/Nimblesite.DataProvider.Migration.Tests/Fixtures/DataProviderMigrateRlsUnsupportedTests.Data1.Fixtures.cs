// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class DataProviderMigrateRlsUnsupportedTests
{
    private const string Data_RlsSchemaYaml_1 = """
            name: sqlserver_rls_guard
            tables:
              - name: documents
                schema: public
                columns:
                  - name: id
                    type: Uuid
                    isNullable: false
                  - name: owner_id
                    type: Uuid
                    isNullable: false
                primaryKey:
                  columns:
                    - id
                rowLevelSecurity:
                  policies:
                    - name: owner_isolation
                      operations:
                        - Select
                      using: owner_id = current_user_id()
            """;
}
