namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [RLS-YAML] from docs/specs/rls-spec.md.

/// <summary>
/// YAML round-trip tests for row-level security policy definitions.
/// </summary>
public sealed class RlsYamlSerializerTests
{
    [Fact]
    public void RlsPolicyDefinition_YamlRoundTrip_Simple()
    {
        var schema = new SchemaDefinition
        {
            Name = "test",
            Tables =
            [
                new TableDefinition
                {
                    Schema = "public",
                    Name = "Documents",
                    Columns =
                    [
                        new ColumnDefinition
                        {
                            Name = "Id",
                            Type = new UuidType(),
                            IsNullable = false,
                        },
                        new ColumnDefinition
                        {
                            Name = "OwnerId",
                            Type = new UuidType(),
                            IsNullable = false,
                        },
                    ],
                    PrimaryKey = new PrimaryKeyDefinition { Columns = ["Id"] },
                    RowLevelSecurity = new RlsPolicySetDefinition
                    {
                        Enabled = true,
                        Policies =
                        [
                            new RlsPolicyDefinition
                            {
                                Name = "owner_isolation",
                                IsPermissive = true,
                                Operations = [RlsOperation.All],
                                UsingLql = "OwnerId = current_user_id()",
                                WithCheckLql = "OwnerId = current_user_id()",
                            },
                        ],
                    },
                },
            ],
        };

        var yaml = SchemaYamlSerializer.ToYaml(schema);
        var deserialized = SchemaYamlSerializer.FromYaml(yaml);

        Assert.Single(deserialized.Tables);
        var table = deserialized.Tables[0];
        Assert.NotNull(table.RowLevelSecurity);
        Assert.True(table.RowLevelSecurity!.Enabled);
        Assert.Single(table.RowLevelSecurity.Policies);

        var policy = table.RowLevelSecurity.Policies[0];
        Assert.Equal("owner_isolation", policy.Name);
        Assert.True(policy.IsPermissive);
        Assert.Single(policy.Operations);
        Assert.Equal(RlsOperation.All, policy.Operations[0]);
        Assert.Equal("OwnerId = current_user_id()", policy.UsingLql);
        Assert.Equal("OwnerId = current_user_id()", policy.WithCheckLql);
    }

    [Fact]
    public void RlsPolicyDefinition_YamlRoundTrip_SubqueryPolicy()
    {
        var subqueryLql = """
            exists(
              UserGroupMemberships
              |> filter(fn(m) => m.UserId = current_user_id() and m.GroupId = GroupId)
            )
            """;

        var yaml = $$"""
            name: app
            tables:
              - name: Documents
                columns:
                  - name: Id
                    type: Uuid
                    isNullable: false
                  - name: GroupId
                    type: Uuid
                rowLevelSecurity:
                  policies:
                    - name: group_read_access
                      operations:
                        - Select
                      using: |
                        {{subqueryLql.Replace("\n", "\n                        ")}}
            """;

        var schema = SchemaYamlSerializer.FromYaml(yaml);

        var policy = schema.Tables[0].RowLevelSecurity!.Policies[0];
        Assert.Equal("group_read_access", policy.Name);
        Assert.Single(policy.Operations);
        Assert.Equal(RlsOperation.Select, policy.Operations[0]);
        Assert.NotNull(policy.UsingLql);
        Assert.Contains("UserGroupMemberships", policy.UsingLql, StringComparison.Ordinal);
        Assert.Contains("current_user_id()", policy.UsingLql, StringComparison.Ordinal);
    }

    [Fact]
    public void RlsOperation_AllValues_SerializeDeserialize()
    {
        foreach (var op in Enum.GetValues<RlsOperation>())
        {
            var schema = new SchemaDefinition
            {
                Name = "t",
                Tables =
                [
                    new TableDefinition
                    {
                        Name = "T",
                        Columns = [new ColumnDefinition { Name = "Id", Type = new UuidType() }],
                        RowLevelSecurity = new RlsPolicySetDefinition
                        {
                            Policies =
                            [
                                new RlsPolicyDefinition
                                {
                                    Name = $"p_{op}",
                                    Operations = [op],
                                    UsingLql = "Id = current_user_id()",
                                },
                            ],
                        },
                    },
                ],
            };

            var yaml = SchemaYamlSerializer.ToYaml(schema);
            var back = SchemaYamlSerializer.FromYaml(yaml);
            Assert.Equal(op, back.Tables[0].RowLevelSecurity!.Policies[0].Operations[0]);
        }
    }

    [Fact]
    public void RlsPolicy_RestrictiveAndRoles_RoundTrip()
    {
        var schema = new SchemaDefinition
        {
            Name = "t",
            Tables =
            [
                new TableDefinition
                {
                    Name = "Audit",
                    Columns = [new ColumnDefinition { Name = "Id", Type = new UuidType() }],
                    RowLevelSecurity = new RlsPolicySetDefinition
                    {
                        Policies =
                        [
                            new RlsPolicyDefinition
                            {
                                Name = "audit_only_admins",
                                IsPermissive = false,
                                Operations = [RlsOperation.Select, RlsOperation.Delete],
                                Roles = ["admin", "auditor"],
                                UsingLql = "true",
                            },
                        ],
                    },
                },
            ],
        };

        var yaml = SchemaYamlSerializer.ToYaml(schema);
        var policy = SchemaYamlSerializer.FromYaml(yaml).Tables[0].RowLevelSecurity!.Policies[0];

        Assert.False(policy.IsPermissive);
        Assert.Equal(2, policy.Operations.Count);
        Assert.Contains(RlsOperation.Select, policy.Operations);
        Assert.Contains(RlsOperation.Delete, policy.Operations);
        Assert.Equal(2, policy.Roles.Count);
        Assert.Contains("admin", policy.Roles);
        Assert.Contains("auditor", policy.Roles);
    }

    [Fact]
    public void RlsPolicySet_DisabledFlag_RoundTrip()
    {
        var schema = new SchemaDefinition
        {
            Name = "t",
            Tables =
            [
                new TableDefinition
                {
                    Name = "T",
                    Columns = [new ColumnDefinition { Name = "Id", Type = new UuidType() }],
                    RowLevelSecurity = new RlsPolicySetDefinition { Enabled = false },
                },
            ],
        };

        var yaml = SchemaYamlSerializer.ToYaml(schema);
        var back = SchemaYamlSerializer.FromYaml(yaml);
        Assert.NotNull(back.Tables[0].RowLevelSecurity);
        Assert.False(back.Tables[0].RowLevelSecurity!.Enabled);
    }

    [Fact]
    public void RlsPolicySet_DefaultsOmittedFromYaml()
    {
        // Defaults: Enabled=true, IsPermissive=true, Operations=[All].
        // These should not appear in serialized YAML.
        var schema = new SchemaDefinition
        {
            Name = "t",
            Tables =
            [
                new TableDefinition
                {
                    Name = "T",
                    Columns = [new ColumnDefinition { Name = "Id", Type = new UuidType() }],
                    RowLevelSecurity = new RlsPolicySetDefinition
                    {
                        Policies =
                        [
                            new RlsPolicyDefinition
                            {
                                Name = "p",
                                UsingLql = "Id = current_user_id()",
                            },
                        ],
                    },
                },
            ],
        };

        var yaml = SchemaYamlSerializer.ToYaml(schema);

        Assert.DoesNotContain("enabled: true", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("isPermissive: true", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void RowLevelSecurity_Absent_DoesNotAppearInYaml()
    {
        var schema = new SchemaDefinition
        {
            Name = "t",
            Tables =
            [
                new TableDefinition
                {
                    Name = "Plain",
                    Columns = [new ColumnDefinition { Name = "Id", Type = new UuidType() }],
                },
            ],
        };

        var yaml = SchemaYamlSerializer.ToYaml(schema);

        Assert.DoesNotContain("rowLevelSecurity", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void RlsPolicy_FullSpecExampleYaml_DeserializesCorrectly()
    {
        // Mirrors the example in [RLS-YAML] from docs/specs/rls-spec.md.
        var yaml = """
            name: MyApp
            tables:
              - name: Documents
                schema: public
                columns:
                  - name: Id
                    type: Uuid
                    isNullable: false
                  - name: OwnerId
                    type: Uuid
                    isNullable: false
                  - name: GroupId
                    type: Uuid
                primaryKey:
                  columns:
                    - Id
                rowLevelSecurity:
                  enabled: true
                  policies:
                    - name: owner_isolation
                      permissive: true
                      operations: [All]
                      roles: []
                      using: "OwnerId = current_user_id()"
                      withCheck: "OwnerId = current_user_id()"
            """;

        var schema = SchemaYamlSerializer.FromYaml(yaml);

        var rls = schema.Tables[0].RowLevelSecurity!;
        Assert.True(rls.Enabled);
        Assert.Single(rls.Policies);
        var policy = rls.Policies[0];
        Assert.Equal("owner_isolation", policy.Name);
        Assert.Equal("OwnerId = current_user_id()", policy.UsingLql);
        Assert.Equal("OwnerId = current_user_id()", policy.WithCheckLql);
    }
}
