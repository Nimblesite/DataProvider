using static global::Tests.Shared.StringAssertions;
// Implements [MIG-TEST-SCHEMA-FIXTURES].
using BodyError = Outcome.Result<
    string,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Error<string, Nimblesite.DataProvider.Migration.Core.MigrationError>;
using BodyOk = Outcome.Result<string, Nimblesite.DataProvider.Migration.Core.MigrationError>.Ok<
    string,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>;

namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial class PostgresFunctionBodyLqlTests
{
    [Theory]
    [MemberData(nameof(TranslatePostgresBody_NapScalarShapes_EmitsSqlBodyInlineCases))]
    public void TranslatePostgresBody_NapScalarShapes_EmitsSqlBody(string bodyLql, string expected)
    {
        var sql = Body(bodyLql);

        Assert.Equal(expected, sql);
    }

    [Fact]
    public void TranslatePostgresBody_ExistsPipeline_EmitsSelectExists()
    {
        var sql = Body(Data_TranslatePostgresBody_ExistsPipeline_EmitsSelectExists_1);

        Assert.StartsWith("SELECT EXISTS (", sql, StringComparison.Ordinal);
        ContainsAll(
            actual: sql,
            expected: ["FROM tenant_members", "user_id = u", "tenant_id = t"],
            comparison: StringComparison.Ordinal
        );
    }

    [Fact]
    public void TranslatePostgresBody_ExistsPipelineWithInList_EmitsInPredicate()
    {
        // Implements [LQL-PREDICATE-IN-LIST].
        var sql = Body(Data_TranslatePostgresBody_ExistsPipelineWithInList_EmitsInPredicate_1);

        Assert.Contains("role IN ('owner', 'admin')", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void FromYaml_FunctionBodyLql_Deserializes()
    {
        var schema = SchemaYamlSerializer.FromYaml(Data_FromYaml_FunctionBodyLql_Deserializes_1);

        Assert.Single(schema.Functions);
        Assert.Equal("current_setting('app.tenant_id')::uuid", schema.Functions[0].BodyLql);
        Assert.Equal(string.Empty, schema.Functions[0].Body);
    }

    [Fact]
    public void FromYaml_FunctionBodyAndBodyLql_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            SchemaYamlSerializer.FromYaml(Data_FromYaml_FunctionBodyAndBodyLql_Throws_1)
        );

        Assert.Contains("mutually exclusive", ex.Message, StringComparison.Ordinal);
        Assert.Contains("public.app_tenant_id", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ToYaml_FunctionBodyLql_EmitsBodyLqlNotBody()
    {
        var yaml = SchemaYamlSerializer.ToYaml(
            Fixture_ToYaml_FunctionBodyLql_EmitsBodyLqlNotBody_1()
        );

        Assert.Contains("bodyLql: current_setting('app.tenant_id')::uuid", yaml);
        Assert.DoesNotContain("body:", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_CreateFunction_BodyLqlCurrentSetting_EmitsDollarQuotedSqlBody()
    {
        var ddl = PostgresDdlGenerator.Generate(
            Dataset_Generate_CreateFunction_BodyLqlCurrentSetting_EmitsDollarQuotedSqlBody_1()
        );

        ContainsAll(
            actual: ddl,
            expected: ["AS $function$", "SELECT current_setting('app.tenant_id', true)::uuid"],
            comparison: StringComparison.Ordinal
        );
        Assert.DoesNotContain("bodyLql", ddl, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_CreateFunction_BodyLqlExists_EmitsSelectExistsBody()
    {
        var ddl = PostgresDdlGenerator.Generate(
            Dataset_Generate_CreateFunction_BodyLqlExists_EmitsSelectExistsBody_1()
        );

        ContainsAll(
            actual: ddl,
            expected: ["SELECT EXISTS (", "FROM tenant_members", "user_id = u", "tenant_id = t"],
            comparison: StringComparison.Ordinal
        );
    }

    [Fact]
    public void Generate_CreateFunction_BodyAndBodyLql_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            PostgresDdlGenerator.Generate(Dataset_Generate_CreateFunction_BodyAndBodyLql_Throws_1())
        );

        Assert.Contains("mutually exclusive", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SchemaDiff_BodyLqlEquivalentToInspectedBody_HasNoOperations()
    {
        var current = Fixture_SchemaDiff_BodyLqlEquivalentToInspectedBody_HasNoOperations_1();
        var desired = current with
        {
            Functions = [Dataset_SchemaDiff_BodyLqlEquivalentToInspectedBody_HasNoOperations_1()],
        };

        var result = SchemaDiff.Calculate(current, desired);

        Assert.True(result is OperationsResultOk);
        Assert.Empty(((OperationsResultOk)result).Value);
    }

    [Fact]
    public void SchemaDiff_BodyLqlChange_EmitsCreateOrReplaceFunction()
    {
        var current = Fixture_SchemaDiff_BodyLqlChange_EmitsCreateOrReplaceFunction_1();
        var desired = current with
        {
            Functions = [Dataset_SchemaDiff_BodyLqlChange_EmitsCreateOrReplaceFunction_1()],
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
