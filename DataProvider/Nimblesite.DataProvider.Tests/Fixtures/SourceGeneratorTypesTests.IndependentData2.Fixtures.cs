// Implements [CI-DESLOP-INDEPENDENT-DATA].

using Nimblesite.Sql.Model;

namespace Nimblesite.DataProvider.Tests;

public partial class SourceGeneratorTypesTests
{
    private static global::System.Collections.Generic.List<global::Nimblesite.Sql.Model.ParameterInfo> Dataset_SqlStatement_CanBeCreatedWithCustomProperties_1() =>
        new List<ParameterInfo> { new("id", "INT"), new("name", "NVARCHAR") };

    private static global::Nimblesite.Sql.Model.JoinRelationship Dataset_JoinRelationship_CanBeCreatedWithCustomJoinType_1() =>
        new JoinRelationship("Invoice", "Customer", "i.CustomerId = c.Id", "LEFT");
}
