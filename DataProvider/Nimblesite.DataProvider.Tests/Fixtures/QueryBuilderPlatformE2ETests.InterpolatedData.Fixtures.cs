// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Tests;

public partial record QueryBuilderPlatformE2ETests
{
    private static string SqlData_CreateProductSchema_1(global::System.String? schema) =>
        $$"""
            name: query_builder_platform
            tables:
              - name: products
                schema: {{schema}}
                columns:
                  - name: id
                    type: Uuid
                    isNullable: false
                  - name: name
                    type: Text
                    isNullable: false
                  - name: price
                    type: Decimal(10,2)
                    isNullable: false
                  - name: category
                    type: Text
                    isNullable: false
                primaryKey:
                  columns:
                    - id
            """;
}
