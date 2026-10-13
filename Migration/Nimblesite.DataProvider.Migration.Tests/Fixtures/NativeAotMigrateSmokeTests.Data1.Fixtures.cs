// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class NativeAotMigrateSmokeTests
{
    private const string Data_Shared_1 = """
        name: aot_smoke
        tables:
          - name: Widget
            columns:
              - name: Id
                type: Text
                isNullable: false
              - name: Label
                type: Text
                isNullable: false
              - name: Quantity
                type: Text
                isNullable: false
            primaryKey:
              columns:
                - Id
            indexes:
              - name: idx_widget_label
                columns:
                  - Label
                isUnique: true
        """;
}
