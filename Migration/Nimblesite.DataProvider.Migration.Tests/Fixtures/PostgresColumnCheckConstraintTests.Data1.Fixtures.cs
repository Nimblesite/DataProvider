// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresColumnCheckConstraintTests
{
    private const string Data_ApplyYaml_WhenColumnCheckConstraintHasName_CreatesNamedPostgresConstraints_1 = """
                name: issue51
                tables:
                  - schema: public
                    name: bundles
                    columns:
                      - name: id
                        type: Uuid
                        isNullable: false
                      - name: tenant_id
                        type: Uuid
                        isNullable: false
                      - name: sha256
                        type: Text
                        isNullable: false
                        checkConstraintName: bundles_sha256_fmt_chk
                        checkConstraint: sha256 ~ '^[0-9a-f]{64}$'
                      - name: size_bytes
                        type: BigInt
                        isNullable: false
                        checkConstraintName: bundles_size_bytes_nonneg_chk
                        checkConstraint: size_bytes >= 0
                    primaryKey:
                      columns:
                        - id
                    uniqueConstraints:
                      - name: uq_bundles_tenant_sha
                        columns:
                          - tenant_id
                          - sha256
                """;

    private const string Data_ConstraintNames_1 = """
            SELECT c.conname
            FROM pg_constraint c
            JOIN pg_class t ON t.oid = c.conrelid
            JOIN pg_namespace n ON n.oid = t.relnamespace
            WHERE n.nspname = @schema
            AND t.relname = 'bundles'
            AND c.conname IN (
                'bundles_sha256_fmt_chk',
                'bundles_size_bytes_nonneg_chk',
                'uq_bundles_tenant_sha'
            )
            ORDER BY c.conname
            """;
}
