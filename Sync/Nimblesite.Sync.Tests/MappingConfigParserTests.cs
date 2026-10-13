using static global::Tests.Shared.ResultAssertions;

namespace Nimblesite.Sync.Tests;

/// <summary>
/// Tests for MappingConfigParser - JSON configuration parsing.
/// Covers spec Section 7.3 - Mapping Configuration Schema.
/// </summary>
public sealed partial class MappingConfigParserTests
{
    private readonly NullLogger<MappingConfigParserTests> _logger = new();

    #region Parse Tests

    [Fact]
    public void Parse_EmptyConfig_ReturnsEmptyMappings()
    {
        var json = """{"version": "1.0", "mappings": []}""";

        var success = ParseConfigAndRequireSuccess(input0: json);
        Assert.Empty(success.Config.Mappings);
        Assert.Equal("1.0", success.Config.Version);
    }

    [Fact]
    public void Parse_SimpleMapping_ParsesCorrectly()
    {
        var success = VerifySharedChecks2(input0: Data_Parse_SimpleMapping_ParsesCorrectly_1);
        Assert.Single(success.Config.Mappings);
        var mapping = success.Config.Mappings[0];
        Assert.Equal("user-mapping", mapping.Id);
        Assert.Equal("User", mapping.SourceTable);
        Assert.Equal("Customer", mapping.TargetTable);
        Assert.Equal(MappingDirection.Push, mapping.Direction);
        Assert.True(mapping.Enabled);
    }

    [Fact]
    public void Parse_PkMapping_ParsesCorrectly()
    {
        var success = VerifySharedChecks2(input0: Data_Parse_PkMapping_ParsesCorrectly_1);
        var mapping = success.Config.Mappings[0];
        Assert.NotNull(mapping.PkMapping);
        Assert.Equal("Id", mapping.PkMapping.SourceColumn);
        Assert.Equal("CustomerId", mapping.PkMapping.TargetColumn);
    }

    [Fact]
    public void Parse_ColumnMappings_ParsesCorrectly()
    {
        var success = VerifySharedChecks2(input0: Data_Parse_ColumnMappings_ParsesCorrectly_1);
        var mapping = success.Config.Mappings[0];
        Assert.Equal(2, mapping.ColumnMappings.Count);
        Assert.Equal("FullName", mapping.ColumnMappings[0].Source);
        Assert.Equal("Name", mapping.ColumnMappings[0].Target);
    }

    [Fact]
    public void Parse_ConstantTransform_ParsesCorrectly()
    {
        var success = VerifySharedChecks2(input0: Data_Parse_ConstantTransform_ParsesCorrectly_1);
        var colMapping = success.Config.Mappings[0].ColumnMappings[0];
        Assert.Null(colMapping.Source);
        Assert.Equal("Source", colMapping.Target);
        Assert.Equal(TransformType.Constant, colMapping.Transform);
        Assert.Equal("mobile-app", colMapping.Value);
    }

    [Fact]
    public void Parse_LqlTransform_ParsesCorrectly()
    {
        var success = VerifySharedChecks2(input0: Data_Parse_LqlTransform_ParsesCorrectly_1);
        var colMapping = success.Config.Mappings[0].ColumnMappings[0];
        Assert.Equal(TransformType.Lql, colMapping.Transform);
        Assert.Contains("dateFormat", colMapping.Lql);
    }

    [Fact]
    public void Parse_ExcludedColumns_ParsesCorrectly()
    {
        var success = VerifySharedChecks2(input0: Data_Parse_ExcludedColumns_ParsesCorrectly_1);
        var excluded = success.Config.Mappings[0].ExcludedColumns;
        Assert.Equal(2, excluded.Count);
        Assert.Contains("PasswordHash", excluded);
        Assert.Contains("SecurityStamp", excluded);
    }

    [Fact]
    public void Parse_Filter_ParsesCorrectly()
    {
        var success = VerifySharedChecks2(input0: Data_Parse_Filter_ParsesCorrectly_1);
        var filter = success.Config.Mappings[0].Filter;
        Assert.NotNull(filter);
        Assert.Contains("IsActive", filter.Lql);
    }

    [Fact]
    public void Parse_SyncTracking_ParsesCorrectly()
    {
        var success = VerifySharedChecks2(input0: Data_Parse_SyncTracking_ParsesCorrectly_1);
        var tracking = success.Config.Mappings[0].SyncTracking;
        Assert.True(tracking.Enabled);
        Assert.Equal(SyncTrackingStrategy.Hash, tracking.Strategy);
        Assert.Equal("_synced_version", tracking.TrackingColumn);
    }

    [Fact]
    public void Parse_MultiTarget_ParsesCorrectly()
    {
        var success = VerifySharedChecks2(input0: Data_Parse_MultiTarget_ParsesCorrectly_1);
        var mapping = success.Config.Mappings[0];
        Assert.True(mapping.IsMultiTarget);
        Assert.NotNull(mapping.Targets);
        Assert.Equal(2, mapping.Targets.Count);
        Assert.Equal("OrderHeader", mapping.Targets[0].Table);
        Assert.Equal("OrderAudit", mapping.Targets[1].Table);
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(Parse_DirectionPull_ParsesCorrectly_ContextCasesData))]
    public void Parse_DirectionPull_ParsesCorrectly_ContextCases(
        string caseName,
        global::System.String value0,
        global::Nimblesite.Sync.Core.MappingDirection value1
    )
    {
        NamedCase(caseName: caseName);
        ExerciseProjectionCaseMatrix6(input0: VerifySharedChecks2, value0: value0, value1: value1);
    }

    [Fact]
    public void Parse_UnmappedTableBehaviorPassthrough_ParsesCorrectly()
    {
        var success = VerifySharedChecks2(
            input0: Data_Parse_UnmappedTableBehaviorPassthrough_ParsesCorrectly_1
        );
        Assert.Equal(UnmappedTableBehavior.Passthrough, success.Config.UnmappedTableBehavior);
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsError()
    {
        var json = """{"this is not valid json""";

        var result = MappingConfigParser.Parse(json, _logger);

        var error = Assert.IsType<MappingConfigParseError>(result);
        Assert.Contains("JSON", error.Error);
    }

    [Fact]
    public void Parse_MissingRequiredFields_SkipsInvalidMapping()
    {
        var success = VerifySharedChecks2(
            input0: Data_Parse_MissingRequiredFields_SkipsInvalidMapping_1
        );
        Assert.Single(success.Config.Mappings);
        Assert.Equal("valid", success.Config.Mappings[0].Id);
    }

    #endregion

    #region ToJson Tests

    [Fact]
    public void ToJson_EmptyConfig_ProducesValidJson()
    {
        var config = SyncMappingConfig.Empty;

        var json = MappingConfigParser.ToJson(config);

        // JSON keys may be PascalCase or camelCase depending on serializer settings
        Assert.True(
            json.Contains("\"version\"", StringComparison.OrdinalIgnoreCase)
                || json.Contains("\"Version\"", StringComparison.OrdinalIgnoreCase)
        );
        Assert.True(
            json.Contains("\"mappings\"", StringComparison.OrdinalIgnoreCase)
                || json.Contains("\"Mappings\"", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void ToJson_RoundTrip_PreservesData()
    {
        var originalJson = Data_ToJson_RoundTrip_PreservesData_1;

        var parseResult = MappingConfigParser.Parse(originalJson, _logger);
        var config = ((MappingConfigParseOk)parseResult).Config;
        var serialized = MappingConfigParser.ToJson(config);
        var success = ParseConfigAndRequireSuccess(input0: serialized);
        Assert.Equal("1.0", success.Config.Version);
        Assert.Single(success.Config.Mappings);
        Assert.Equal("test-mapping", success.Config.Mappings[0].Id);
        Assert.Equal("User", success.Config.Mappings[0].SourceTable);
    }

    #endregion

    #region Full Spec Example

    [Fact]
    public void Parse_FullSpecExample_ParsesCorrectly()
    {
        // This is the example from spec Section 7.3
        var json = Data_Parse_FullSpecExample_ParsesCorrectly_1;

        var success = ParseConfigAndRequireSuccess(input0: json);
        var mapping = success.Config.Mappings[0];

        Assert.Equal("user-to-customer", mapping.Id);
        Assert.Equal("User", mapping.SourceTable);
        Assert.Equal("Customer", mapping.TargetTable);
        Assert.Equal(MappingDirection.Push, mapping.Direction);
        Assert.True(mapping.Enabled);
        Assert.NotNull(mapping.PkMapping);
        Assert.Equal("Id", mapping.PkMapping.SourceColumn);
        Assert.Equal("CustomerId", mapping.PkMapping.TargetColumn);
        Assert.Equal(4, mapping.ColumnMappings.Count);
        Assert.Equal(2, mapping.ExcludedColumns.Count);
        Assert.NotNull(mapping.Filter);
        Assert.True(mapping.SyncTracking.Enabled);
        Assert.Equal(SyncTrackingStrategy.Version, mapping.SyncTracking.Strategy);
    }

    #endregion

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private global::Nimblesite.Sync.Core.MappingConfigParseOk ParseConfigAndRequireSuccess(
        global::System.String input0
    )
    {
        var result = MappingConfigParser.Parse(input0, _logger);

        var success = Assert.IsType<MappingConfigParseOk>(result);

        return success;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private MappingConfigParseOk VerifySharedChecks2(string input0)
    {
        var json = input0;

        var success = ParseConfigAndRequireSuccess(input0: json);

        return success;
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    private void ExerciseProjectionCaseMatrix6(
        Func<string, MappingConfigParseOk> input0,
        global::System.String value0,
        global::Nimblesite.Sync.Core.MappingDirection value1
    )
    {
        var success = input0(arg: value0);
        Assert.Equal(value1, success.Config.Mappings[0].Direction);
    }
}
