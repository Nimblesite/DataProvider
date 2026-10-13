// Implements [CI-TEST-INDEPENDENT-SCHEMA-DATA].
// Pure immutable fixture definitions; assertions and database workflows remain analysed.

namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial class PostgresCheckConstraintRerunTests
{
    private static SchemaDefinition Schema() =>
        SchemaTestData.Schema(
            name: "optional_check_reruns",
            tables:
            [
                SchemaTestData.Table(
                    name: "agent_configs",
                    columns:
                    [
                        SchemaTestData.Column(
                            name: "id",
                            type: PortableTypes.Uuid,
                            isNullable: false
                        ),
                        Checked(
                            "workspace_host_kind",
                            "workspace_host_kind IS NULL OR workspace_host_kind IN ('fly','docker')"
                        ),
                        Checked(
                            "workspace_size",
                            "workspace_size IS NULL OR workspace_size IN ('small','medium','large')"
                        ),
                        Checked(
                            "provisioning_bundle_sha256",
                            "provisioning_bundle_sha256 IS NULL OR provisioning_bundle_sha256 ~ '^[0-9a-f]{64}$'"
                        ),
                        SchemaTestData.Column(
                            name: "workspace_dev_access",
                            type: PortableTypes.Boolean,
                            checkConstraint: "workspace_dev_access = false"
                        ),
                    ],
                    primaryKey: new PrimaryKeyDefinition { Columns = ["id"] }
                ),
            ]
        );
}
