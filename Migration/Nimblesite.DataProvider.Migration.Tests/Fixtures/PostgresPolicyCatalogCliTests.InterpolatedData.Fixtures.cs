// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresPolicyCatalogCliTests
{
    private static string SqlData_PolicyYaml_1(
        global::System.String? check,
        global::System.String? predicate,
        global::System.Boolean sql
    ) =>
        $$"""
            name: nap_policy_repro
            tables:
              - name: items
                schema: public
                columns:
                  - name: id
                    type: Int
                    isNullable: false
                primaryKey:
                  columns: [id]
                rowLevelSecurity:
                  enabled: true
                  forced: true
                  policies:
                    - name: item_visible
                      permissive: true
                      operations: [{{(check is null ? "Select" : predicate is null ? "Insert" : "All")}}]
                      roles: [PUBLIC]
                      {{(predicate is null ? "" : $"{(sql ? "usingSql" : "using")}: '{predicate}'")}}
                      {{(
                check is null ? "" : $"{(sql ? "withCheckSql" : "withCheck")}: '{check}'"
            )}}
            """;
}
