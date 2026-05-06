using BodyError = Outcome.Result<
    string,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Error<string, Nimblesite.DataProvider.Migration.Core.MigrationError>;
using BodyOk = Outcome.Result<string, Nimblesite.DataProvider.Migration.Core.MigrationError>.Ok<
    string,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>;

namespace Nimblesite.DataProvider.Migration.Tests;

public sealed class PostgresFunctionBodyLqlTests
{
    [Theory]
    [InlineData(
        "current_setting('app.tenant_id')::uuid",
        "SELECT current_setting('app.tenant_id', true)::uuid"
    )]
    [InlineData(
        "SELECT current_setting('app.user_id')::uuid",
        "SELECT current_setting('app.user_id', true)::uuid"
    )]
    [InlineData("true", "SELECT true")]
    [InlineData(
        "is_member(current_setting('app.user_id')::uuid, current_setting('app.tenant_id')::uuid)",
        "SELECT is_member(current_setting('app.user_id', true)::uuid, current_setting('app.tenant_id', true)::uuid)"
    )]
    public void TranslatePostgresBody_NapScalarShapes_EmitsSqlBody(string bodyLql, string expected)
    {
        var sql = Body(bodyLql);

        Assert.Equal(expected, sql);
    }

    [Fact]
    public void TranslatePostgresBody_ExistsPipeline_EmitsSelectExists()
    {
        var sql = Body(
            """
            exists(
              tenant_members
              |> filter(fn(m) => m.user_id = u and m.tenant_id = t)
            )
            """
        );

        Assert.StartsWith("SELECT EXISTS (", sql, StringComparison.Ordinal);
        Assert.Contains("FROM tenant_members", sql, StringComparison.Ordinal);
        Assert.Contains("user_id = u", sql, StringComparison.Ordinal);
        Assert.Contains("tenant_id = t", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void FromYaml_FunctionBodyLql_Deserializes()
    {
        var schema = SchemaYamlSerializer.FromYaml(
            """
            name: nap
            functions:
              - schema: public
                name: app_tenant_id
                returns: uuid
                bodyLql: current_setting('app.tenant_id')::uuid
            tables: []
            """
        );

        Assert.Single(schema.Functions);
        Assert.Equal("current_setting('app.tenant_id')::uuid", schema.Functions[0].BodyLql);
        Assert.Equal(string.Empty, schema.Functions[0].Body);
    }

    [Fact]
    public void FromYaml_FunctionBodyAndBodyLql_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            SchemaYamlSerializer.FromYaml(
                """
                name: nap
                functions:
                  - name: app_tenant_id
                    returns: uuid
                    body: SELECT NULL::uuid
                    bodyLql: current_setting('app.tenant_id')::uuid
                tables: []
                """
            )
        );

        Assert.Contains("mutually exclusive", ex.Message, StringComparison.Ordinal);
        Assert.Contains("public.app_tenant_id", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ToYaml_FunctionBodyLql_EmitsBodyLqlNotBody()
    {
        var yaml = SchemaYamlSerializer.ToYaml(
            new SchemaDefinition
            {
                Name = "nap",
                Functions =
                [
                    new PostgresFunctionDefinition
                    {
                        Name = "app_tenant_id",
                        Returns = "uuid",
                        BodyLql = "current_setting('app.tenant_id')::uuid",
                    },
                ],
            }
        );

        Assert.Contains("bodyLql: current_setting('app.tenant_id')::uuid", yaml);
        Assert.DoesNotContain("body:", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_CreateFunction_BodyLqlCurrentSetting_EmitsDollarQuotedSqlBody()
    {
        var ddl = PostgresDdlGenerator.Generate(
            new CreateOrReplaceFunctionOperation(
                new PostgresFunctionDefinition
                {
                    Name = "app_tenant_id",
                    Returns = "uuid",
                    BodyLql = "current_setting('app.tenant_id')::uuid",
                }
            )
        );

        Assert.Contains("AS $function$", ddl, StringComparison.Ordinal);
        Assert.Contains(
            "SELECT current_setting('app.tenant_id', true)::uuid",
            ddl,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("bodyLql", ddl, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_CreateFunction_BodyLqlExists_EmitsSelectExistsBody()
    {
        var ddl = PostgresDdlGenerator.Generate(
            new CreateOrReplaceFunctionOperation(
                new PostgresFunctionDefinition
                {
                    Name = "is_member",
                    Returns = "boolean",
                    Arguments =
                    [
                        new PostgresFunctionArgumentDefinition { Name = "u", Type = "uuid" },
                        new PostgresFunctionArgumentDefinition { Name = "t", Type = "uuid" },
                    ],
                    SecurityDefiner = true,
                    BodyLql =
                        "exists(tenant_members |> filter(fn(m) => m.user_id = u and m.tenant_id = t))",
                }
            )
        );

        Assert.Contains("SELECT EXISTS (", ddl, StringComparison.Ordinal);
        Assert.Contains("FROM tenant_members", ddl, StringComparison.Ordinal);
        Assert.Contains("user_id = u", ddl, StringComparison.Ordinal);
        Assert.Contains("tenant_id = t", ddl, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_CreateFunction_BodyAndBodyLql_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            PostgresDdlGenerator.Generate(
                new CreateOrReplaceFunctionOperation(
                    new PostgresFunctionDefinition
                    {
                        Name = "bad",
                        Body = "SELECT true",
                        BodyLql = "true",
                    }
                )
            )
        );

        Assert.Contains("mutually exclusive", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SchemaDiff_BodyLqlEquivalentToInspectedBody_HasNoOperations()
    {
        var current = new SchemaDefinition
        {
            Name = "nap",
            Functions =
            [
                new PostgresFunctionDefinition
                {
                    Name = "app_tenant_id",
                    Returns = "uuid",
                    Body = "SELECT current_setting('app.tenant_id', true)::uuid",
                },
            ],
        };
        var desired = current with
        {
            Functions =
            [
                new PostgresFunctionDefinition
                {
                    Name = "app_tenant_id",
                    Returns = "uuid",
                    BodyLql = "current_setting('app.tenant_id')::uuid",
                },
            ],
        };

        var result = SchemaDiff.Calculate(current, desired);

        Assert.True(result is OperationsResultOk);
        Assert.Empty(((OperationsResultOk)result).Value);
    }

    [Fact]
    public void SchemaDiff_BodyLqlChange_EmitsCreateOrReplaceFunction()
    {
        var current = new SchemaDefinition
        {
            Name = "nap",
            Functions =
            [
                new PostgresFunctionDefinition
                {
                    Name = "app_tenant_id",
                    Returns = "uuid",
                    Body = "SELECT current_setting('app.tenant_id', true)::uuid",
                },
            ],
        };
        var desired = current with
        {
            Functions =
            [
                new PostgresFunctionDefinition
                {
                    Name = "app_tenant_id",
                    Returns = "text",
                    BodyLql = "current_setting('app.tenant_id')",
                },
            ],
        };

        var result = SchemaDiff.Calculate(current, desired);

        Assert.True(result is OperationsResultOk);
        Assert.Contains(
            ((OperationsResultOk)result).Value,
            op => op is CreateOrReplaceFunctionOperation
        );
    }

    private static string Body(string bodyLql)
    {
        var result = LqlFunctionBodyTranspiler.TranslatePostgresBody(bodyLql, "public.test");
        Assert.True(result is BodyOk, result is BodyError e ? e.Value.Message : "expected Ok");
        return ((BodyOk)result).Value;
    }
}
