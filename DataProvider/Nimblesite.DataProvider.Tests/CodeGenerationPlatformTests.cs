using System.Collections;
using System.Data.Common;
using System.Reflection;
using Nimblesite.DataProvider.SQLite;
using Nimblesite.DataProvider.SqlServer;
using Nimblesite.Sql.Model;
using Tests.Shared;
using static global::Tests.Shared.ResultAssertions;
using CodeOk = Outcome.Result<string, Nimblesite.Sql.Model.SqlError>.Ok<
    string,
    Nimblesite.Sql.Model.SqlError
>;
using ColumnsOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.DataProvider.Core.DatabaseColumn>,
    Nimblesite.Sql.Model.SqlError
>.Ok<
    System.Collections.Generic.IReadOnlyList<Nimblesite.DataProvider.Core.DatabaseColumn>,
    Nimblesite.Sql.Model.SqlError
>;
using ParameterInfo = Nimblesite.Sql.Model.ParameterInfo;

namespace Nimblesite.DataProvider.Tests;

public sealed partial record QueryBuilderPlatformE2ETests
{
    private static readonly string[] GeneratedExpectedNames =
    [
        "Gadget X",
        "Gadget Y",
        "Premium Widget",
        "Tool Beta",
    ];

    // Implements [CON-SHARED-CORE]. The same generated query, insert and update execute on every platform.
    private static async Task AssertGeneratedCodeAsync(DbConnection connection, string provider)
    {
        var parameter = provider == "sqlserver" ? "@threshold" : "@price";
        var sql =
            $"SELECT name AS name, price AS price, CASE WHEN price >= 100 THEN NULL ELSE price END AS optional FROM products WHERE ({parameter} IS NULL OR price >= {parameter}) ORDER BY name";
        if (provider == "sqlserver")
            sql = $"BEGIN DECLARE @threshold decimal(18,2) = @price; {sql}; END";
        var statement = new SelectStatement { Parameters = [new ParameterInfo(Name: "price")] };
        var source =
            provider == "sqlserver"
                ? await SqlServerCodeGenerator
                    .GenerateCodeAsync(
                        fileName: "GetProducts",
                        sql: sql,
                        connectionString: connection.ConnectionString
                    )
                    .ConfigureAwait(true)
                : await GenerateQueryAsync(connection: connection, sql: sql, statement: statement)
                    .ConfigureAwait(true);
        var generatedSource = ResultAssertions
            .Expected<CodeOk>(actual: source, message: source.ToString())
            .Value;
        Assert.Contains("object? price", generatedSource);
        var assembly = GeneratedCodeCompiler.Compile(source: generatedSource);
        var values = await GeneratedCodeCompiler
            .InvokeAsync(
                assembly: assembly,
                type: "Generated.GetProductsExtensions",
                method: "GetProductsAsync",
                arguments: [connection, 40m]
            )
            .ConfigureAwait(true);
        var rows = Assert.IsAssignableFrom<IEnumerable>(values).Cast<object>().ToArray();
        Assert.Equal(
            GeneratedExpectedNames,
            rows.Select(row => row.GetType().GetProperty(name: "name")?.GetValue(obj: row))
        );
        Assert.Null(rows[2].GetType().GetProperty(name: "optional")?.GetValue(obj: rows[2]));
        Assert.NotNull(rows[0].GetType().GetProperty(name: "optional")?.GetValue(obj: rows[0]));
        var unfiltered = await GeneratedCodeCompiler
            .InvokeAsync(
                assembly: assembly,
                type: "Generated.GetProductsExtensions",
                method: "GetProductsAsync",
                arguments: [connection, null]
            )
            .ConfigureAwait(true);
        Assert.Equal(8, Assert.IsAssignableFrom<IEnumerable>(unfiltered).Cast<object>().Count());
        await AssertGeneratedCrudAsync(connection: connection, provider: provider)
            .ConfigureAwait(true);
        await AssertGeneratedDeleteAsync(connection: connection, provider: provider)
            .ConfigureAwait(true);
    }

    private static async Task<Outcome.Result<string, SqlError>> GenerateQueryAsync(
        DbConnection connection,
        string sql,
        SelectStatement statement
    )
    {
        var columns = await ReadColumnsAsync(
                connection: connection,
                sql: sql,
                parameters: statement.Parameters
            )
            .ConfigureAwait(true);
        var priceColumn = ResultAssertions
            .Expected<ColumnsOk>(actual: columns, message: columns.ToString())
            .Value.Single(column => column.Name == "price");
        Assert.True(
            priceColumn.CSharpType != "string" && priceColumn.CSharpType != "string?",
            $"Numeric price mapped to {priceColumn.CSharpType}; SQL type {priceColumn.SqlType}"
        );
        var config = new CodeGenerationConfig(
            getColumnMetadata: (_, _, _) => Task.FromResult(columns)
        )
        {
            ConnectionType = connection.GetType().FullName ?? "DbConnection",
        };
        return SqlAntlrCodeGenerator.GenerateCodeWithMetadata(
            fileName: "GetProducts",
            sql: sql,
            statement: statement,
            columnMetadata: ResultAssertions
                .Expected<ColumnsOk>(actual: columns, message: columns.ToString())
                .Value,
            config: config
        );
    }

    private static Task<Outcome.Result<IReadOnlyList<DatabaseColumn>, SqlError>> ReadColumnsAsync(
        DbConnection connection,
        string sql,
        IEnumerable<ParameterInfo> parameters
    ) =>
        connection is Microsoft.Data.Sqlite.SqliteConnection
            ? SqliteCodeGenerator.GetColumnMetadataFromSqlAsync(
                connectionString: connection.ConnectionString,
                sql: sql,
                parameters: parameters
            )
            : Nimblesite.DataProvider.Postgres.PostgresCodeGenerator.GetColumnMetadataFromSqlAsync(
                connectionString: connection.ConnectionString,
                sql: sql,
                parameters: parameters
            );

    private static async Task AssertGeneratedCrudAsync(DbConnection connection, string provider)
    {
        var columns =
            provider == "sqlserver"
                ? await SqlServerCodeGenerator
                    .CreateConfig()
                    .GetColumnMetadata(connection.ConnectionString, "SELECT * FROM products", [])
                    .ConfigureAwait(true)
                : await ReadColumnsAsync(
                        connection: connection,
                        sql: "SELECT * FROM products",
                        parameters: []
                    )
                    .ConfigureAwait(true);
        var table = new DatabaseTable
        {
            Name = "products",
            Schema = provider switch
            {
                "sqlite" => "main",
                "postgres" => "public",
                _ => "dbo",
            },
            Columns = ResultAssertions
                .Expected<ColumnsOk>(actual: columns, message: columns.ToString())
                .Value.Select(column =>
                    column with
                    {
                        IsPrimaryKey = column.Name.Equals("id", StringComparison.OrdinalIgnoreCase),
                        IsIdentity = false,
                    }
                )
                .ToArray(),
        };
        var source = new DefaultTableOperationGenerator(
            connectionType: connection.GetType().Name
        ).GenerateTableOperations(
            table: table,
            config: new TableConfig
            {
                Name = "products",
                GenerateInsert = true,
                GenerateUpdate = true,
                GenerateDelete = true,
            }
        );
        var assembly = GeneratedCodeCompiler.Compile(
            source: Ok(actual: source, message: source.ToString()).Value
        );
        using var transaction = await connection.BeginTransactionAsync().ConfigureAwait(true);
        Assert.Equal(
            1,
            await InvokeCrudAsync(
                    assembly: assembly,
                    transaction: transaction,
                    operation: "Insert",
                    name: "Generated O'Brien 雪"
                )
                .ConfigureAwait(true)
        );
        Assert.Equal(
            1,
            await InvokeCrudAsync(
                    assembly: assembly,
                    transaction: transaction,
                    operation: "Update",
                    name: "Changed"
                )
                .ConfigureAwait(true)
        );
        Assert.Equal(
            1,
            await InvokeCrudAsync(
                    assembly: assembly,
                    transaction: transaction,
                    operation: "Delete",
                    name: "Changed"
                )
                .ConfigureAwait(true)
        );
        Assert.Equal(
            1,
            await InvokeCrudAsync(
                    assembly: assembly,
                    transaction: transaction,
                    operation: "Insert",
                    name: "Changed"
                )
                .ConfigureAwait(true)
        );
        await transaction.CommitAsync().ConfigureAwait(true);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM products WHERE name = 'Changed'";
        Assert.Equal(
            1L,
            Convert.ToInt64(
                await command.ExecuteScalarAsync().ConfigureAwait(true),
                System.Globalization.CultureInfo.InvariantCulture
            )
        );
    }

    private static async Task AssertGeneratedDeleteAsync(DbConnection connection, string provider)
    {
        const string sql = "DELETE FROM products WHERE name = @name";
        var methods = DataAccessGenerator.GenerateNonQueryMethod(
            className: "DeleteProductsExtensions",
            methodName: "DeleteProducts",
            sql: sql,
            parameters: [new ParameterInfo(Name: "name")],
            connectionType: connection.GetType().FullName ?? "DbConnection"
        );
        var source =
            provider == "sqlserver"
                ? await SqlServerCodeGenerator
                    .GenerateCodeAsync(
                        fileName: "DeleteProducts",
                        sql: sql,
                        connectionString: connection.ConnectionString
                    )
                    .ConfigureAwait(true)
                : CodeGenerationConfig.GenerateSourceForConnection(
                    namespaceName: "Generated",
                    modelCode: string.Empty,
                    dataAccessCode: Ok(actual: methods, message: methods.ToString()).Value,
                    connectionNamespace: connection.GetType().Namespace ?? "System.Data.Common"
                );
        var assembly = GeneratedCodeCompiler.Compile(
            source: Ok(actual: source, message: source.ToString()).Value
        );
        Assert.Equal(
            1,
            await GeneratedCodeCompiler
                .InvokeAsync(
                    assembly: assembly,
                    type: "Generated.DeleteProductsExtensions",
                    method: "DeleteProductsAsync",
                    arguments: [connection, "Changed"]
                )
                .ConfigureAwait(true)
        );
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM products WHERE name = 'Changed'";
        Assert.Equal(
            0L,
            Convert.ToInt64(
                await command.ExecuteScalarAsync().ConfigureAwait(true),
                System.Globalization.CultureInfo.InvariantCulture
            )
        );
    }

    private static async Task<object?> InvokeCrudAsync(
        Assembly assembly,
        DbTransaction transaction,
        string operation,
        string name
    )
    {
        var member = Assert.IsAssignableFrom<MethodInfo>(
            assembly
                .GetType(name: "Generated.productsExtensions")
                ?.GetMethod(name: $"{operation}productsAsync")
        );
        var arguments = member
            .GetParameters()
            .Select(parameter =>
                parameter.Name switch
                {
                    "transaction" => (object)transaction,
                    "name" => name,
                    "category" => "Tools",
                    "id"
                        when (
                            Nullable.GetUnderlyingType(parameter.ParameterType)
                            ?? parameter.ParameterType
                        ) == typeof(Guid) => Guid.Parse("427a5ac1-ddf9-47a8-869e-80f6252f56ef"),
                    "id" => Convert.ChangeType(
                        42,
                        Nullable.GetUnderlyingType(parameter.ParameterType)
                            ?? parameter.ParameterType,
                        System.Globalization.CultureInfo.InvariantCulture
                    ),
                    _ => Convert.ChangeType(
                        42,
                        Nullable.GetUnderlyingType(parameter.ParameterType)
                            ?? parameter.ParameterType,
                        System.Globalization.CultureInfo.InvariantCulture
                    ),
                }
            )
            .ToArray();
        return await GeneratedCodeCompiler
            .InvokeAsync(
                assembly: assembly,
                type: "Generated.productsExtensions",
                method: member.Name,
                arguments: arguments
            )
            .ConfigureAwait(true);
    }
}
