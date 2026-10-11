// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.Sync.Integration.Tests;

public partial class HttpMappingSyncTests
{
    private static global::System.Collections.Generic.List<global::Nimblesite.Sync.Core.ColumnMapping> Dataset_ConstantTransform_SpecialValues_Work_1() =>
        new List<ColumnMapping>
        {
            new("FullName", "name"),
            new(null, "status", TransformType.Constant, "active"),
            new(null, "priority", TransformType.Constant, "0"),
            new(null, "verified", TransformType.Constant, "true"),
            new(null, "notes", TransformType.Constant, ""),
        };
}
