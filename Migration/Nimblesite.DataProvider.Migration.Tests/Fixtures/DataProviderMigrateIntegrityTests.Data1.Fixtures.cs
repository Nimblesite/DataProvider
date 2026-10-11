// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class DataProviderMigrateIntegrityTests
{
    private const string Data_Migrate_WhenDiffIsCleanButIntegrityFails_DoesNotClaimSchemaUpToDate_1 = """
            name: nap
            tables:
              - name: settings
                schema: public
                columns:
                  - name: id
                    type: Uuid
                    isNullable: false
                  - name: tier
                    type: Text
                    isNullable: false
                    defaultValue: "'pro'"
                primaryKey:
                  columns:
                    - id
            """;

    private const string Data_DesiredAgentConfigsYaml_1 = """
            name: nap
            tables:
              - name: agent_configs
                schema: public
                columns:
                  - name: id
                    type: Uuid
                    isNullable: false
                  - name: tenant_id
                    type: Uuid
                    isNullable: false
                  - name: name
                    type: Text
                    isNullable: false
                primaryKey:
                  columns:
                    - id
            """;

    private const string Data_DesiredAgentConfigsWithUniqueYaml_1 = """
            name: nap
            tables:
              - name: agent_configs
                schema: public
                columns:
                  - name: id
                    type: Uuid
                    isNullable: false
                  - name: tenant_id
                    type: Uuid
                    isNullable: false
                  - name: name
                    type: Text
                    isNullable: false
                primaryKey:
                  columns:
                    - id
                uniqueConstraints:
                  - name: uq_agent_configs_tenant_name
                    columns:
                      - tenant_id
                      - name
            """;
}
