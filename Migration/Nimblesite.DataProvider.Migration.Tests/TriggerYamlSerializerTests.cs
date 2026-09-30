namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [MIG-TRIGGER-YAML] from docs/specs/declarative-triggers-spec.md
// (GitHub issue 82).

/// <summary>
/// YAML round-trip tests for declarative trigger definitions.
/// </summary>
public sealed class TriggerYamlSerializerTests
{
    private const string TriggerYaml = """
        name: tenant_app
        tables:
          - name: tenant_members
            columns:
              - name: id
                type: Uuid
                isNullable: false
              - name: tenant_id
                type: Uuid
                isNullable: false
              - name: role
                type: VarChar(50)
                isNullable: false
            primaryKey:
              columns:
                - id
            triggers:
              - name: assert_not_last_owner
                timing: Before
                events: [Update, Delete]
                forEachRow: true
                raiseWhen: |
                  old.role = 'owner' and not exists(
                    tenant_members
                    |> filter(fn(m) => m.tenant_id = old.tenant_id and m.role = 'owner' and m.id <> old.id)
                  )
                errorMessage: cannot remove the last owner of a tenant
        """;

    [Fact]
    public void TriggerDefinition_YamlRoundTrip_PreservesDeclaration()
    {
        var schema = SchemaYamlSerializer.FromYaml(TriggerYaml);
        var back = SchemaYamlSerializer.ToYaml(schema);

        Assert.Contains("triggers:", back, StringComparison.Ordinal);
        Assert.Contains("assert_not_last_owner", back, StringComparison.Ordinal);
        Assert.Contains("Update", back, StringComparison.Ordinal);
        Assert.Contains("Delete", back, StringComparison.Ordinal);
        Assert.Contains("raiseWhen", back, StringComparison.Ordinal);
        Assert.Contains("not exists", back, StringComparison.Ordinal);
        Assert.Contains("errorMessage", back, StringComparison.Ordinal);
        Assert.Contains("cannot remove the last owner", back, StringComparison.Ordinal);
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
        var schema = new SchemaDefinition
        {
            Name = "t",
            Tables =
            [
                new TableDefinition
                {
                    Name = "plain",
                    Columns = [new ColumnDefinition { Name = "id", Type = new UuidType() }],
                },
            ],
        };

        var yaml = SchemaYamlSerializer.ToYaml(schema);

        Assert.DoesNotContain("triggers", yaml, StringComparison.Ordinal);
    }
}
