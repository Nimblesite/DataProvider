using static global::Tests.Shared.StringAssertions;

// Implements [MIG-TEST-SCHEMA-FIXTURES].
namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [MIG-TRIGGER-YAML] from docs/specs/declarative-triggers-spec.md
// (GitHub issue 82).

/// <summary>
/// YAML round-trip tests for declarative trigger definitions.
/// </summary>
public sealed partial class TriggerYamlSerializerTests
{
    private const string TriggerYaml = Data_Shared_1;

    [Fact]
    public void TriggerDefinition_YamlRoundTrip_PreservesDeclaration()
    {
        var schema = SchemaYamlSerializer.FromYaml(TriggerYaml);
        var back = SchemaYamlSerializer.ToYaml(schema);

        ContainsAll(
            actual: back,
            expected:
            [
                "triggers:",
                "assert_not_last_owner",
                "Update",
                "Delete",
                "raiseWhen",
                "not exists",
                "errorMessage",
                "cannot remove the last owner",
            ],
            comparison: StringComparison.Ordinal
        );
    }

    [Fact]
    public void TriggerDefinition_YamlRoundTrip_OmitsDefaults()
    {
        // Defaults: timing Before, forEachRow true. These must not appear in
        // serialized YAML, and a second round-trip must preserve the trigger.
        var schema = SchemaYamlSerializer.FromYaml(TriggerYaml);
        var back = SchemaYamlSerializer.ToYaml(schema);

        Assert.DoesNotContain("timing:", back, StringComparison.Ordinal);
        Assert.DoesNotContain("forEachRow:", back, StringComparison.Ordinal);

        var reparsed = SchemaYamlSerializer.FromYaml(back);
        var rereserialized = SchemaYamlSerializer.ToYaml(reparsed);
        Assert.Contains("assert_not_last_owner", rereserialized, StringComparison.Ordinal);
    }

    [Fact]
    public void Triggers_Absent_DoNotAppearInYaml()
    {
        var yaml = VerifyCrossClassChecks6(input0: Fixture_Triggers_Absent_DoNotAppearInYaml_1);

        Assert.DoesNotContain("triggers", yaml, StringComparison.Ordinal);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static global::System.String VerifyCrossClassChecks6(Func<SchemaDefinition> input0)
    {
        var schema = input0();

        var yaml = SchemaYamlSerializer.ToYaml(schema);

        return yaml;
    }
}
