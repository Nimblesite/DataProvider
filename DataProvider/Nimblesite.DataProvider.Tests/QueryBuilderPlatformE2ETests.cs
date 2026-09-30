using System.Collections.Immutable;
using System.Data;
using System.Data.Common;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Nimblesite.Sql.Model;
using Npgsql;
using NpgsqlTypes;
using NamesOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<string>,
    Nimblesite.Sql.Model.SqlError
>.Ok<System.Collections.Generic.IReadOnlyList<string>, Nimblesite.Sql.Model.SqlError>;
using ProductRowsOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<(string Name, decimal Price)>,
    Nimblesite.Sql.Model.SqlError
>.Ok<
    System.Collections.Generic.IReadOnlyList<(string Name, decimal Price)>,
    Nimblesite.Sql.Model.SqlError
>;

namespace Nimblesite.DataProvider.Tests;

public sealed record QueryBuilderPlatformE2ETests : IClassFixture<QueryBuilderDatabaseContainers>
{
    private readonly QueryBuilderDatabaseContainers _containers;

    public QueryBuilderPlatformE2ETests(QueryBuilderDatabaseContainers containers)
    {
        _containers = containers;
    }

    private static readonly ImmutableArray<(
        string Name,
        decimal Price,
        string Category
    )> Products =
    [
        ("Widget A", 10m, "Electronics"),
        ("Widget B", 25.5m, "Electronics"),
        ("Gadget X", 99.99m, "Gadgets"),
        ("Gadget Y", 149.99m, "Gadgets"),
        ("Tool Alpha", 35m, "Tools"),
        ("Tool Beta", 45m, "Tools"),
        ("Tool Gamma", 15m, "Tools"),
        ("Premium Widget", 500m, "Electronics"),
    ];

    // Implements [LQL-OUTPUT-DIALECTS]. The same builder and model queries must execute on all platforms.
    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    public async Task ProductFilterAndPagination_ReturnExactRows(string provider)
    {
        if (provider == "sqlite")
        {
            using var seeded = new QueryBuilderE2ETests();
            AssertProductQueries(seeded.Connection, provider);
            return;
        }

        var connectionString = await _containers
            .GetConnectionStringAsync(provider)
            .ConfigureAwait(true);
        CreateProductSchema(provider, connectionString);
        if (provider == "postgres")
        {
            using var postgres = new NpgsqlConnection(connectionString);
            await postgres.OpenAsync().ConfigureAwait(true);
            SeedPostgres(postgres);
            AssertProductQueries(postgres, provider);
            return;
        }
        Assert.Equal("sqlserver", provider);
        using var sqlServer = new SqlConnection(connectionString);
        await sqlServer.OpenAsync().ConfigureAwait(true);
        SeedSqlServer(sqlServer);
        AssertProductQueries(sqlServer, provider);
    }

    private static void AssertProductQueries(DbConnection connection, string provider)
    {
        var filtered = "products"
            .From()
            .Select(columns: [(null, "name"), (null, "price")])
            .Where(columnName: "category", value: "Tools")
            .OrderBy(columnName: "price")
            .Take(count: 2)
            .ToSqlStatement();
        var toolSql = GenerateSql(filtered, provider);
        var tools = ReadProducts(connection, toolSql);
        Assert.Equal(2, tools.Length);
        Assert.Equal(("Tool Gamma", 15m), tools[0]);
        Assert.Equal(("Tool Alpha", 35m), tools[1]);
        AssertProductPages(connection, provider);
    }

    private static void AssertProductPages(DbConnection connection, string provider)
    {
        var first = ReadNames(connection, ModelPageSql(provider, offset: 0));
        var second = ReadNames(connection, ModelPageSql(provider, offset: 3));
        var third = ReadNames(connection, ModelPageSql(provider, offset: 6));
        Assert.Equal(3, first.Length);
        Assert.Equal("Gadget X", first[0]);
        Assert.Equal("Gadget Y", first[1]);
        Assert.Equal("Premium Widget", first[2]);
        Assert.Equal(3, second.Length);
        Assert.Equal("Tool Alpha", second[0]);
        Assert.Equal("Tool Beta", second[1]);
        Assert.Equal("Tool Gamma", second[2]);
        Assert.Equal(2, third.Length);
        Assert.Equal("Widget A", third[0]);
        Assert.Equal("Widget B", third[1]);
        Assert.Equal(8, first.Length + second.Length + third.Length);
        Assert.Equal(8, first.Concat(second).Concat(third).Distinct().Count());
    }

    private static string ModelPageSql(string provider, int offset)
    {
        var statement = new SelectStatementBuilder()
            .AddTable(name: "products")
            .AddSelectColumn(name: "name")
            .AddOrderBy(column: "name", direction: "ASC")
            .WithLimit("3")
            .WithOffset(offset.ToString(CultureInfo.InvariantCulture))
            .Build();
        var sql = GenerateSql(statement, provider);
        Assert.Contains("SELECT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("products", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            provider == "sqlserver" ? "OFFSET" : "LIMIT",
            sql,
            StringComparison.OrdinalIgnoreCase
        );
        return sql;
    }

    private static string GenerateSql(SelectStatement statement, string provider) =>
        SqlStatementGenerationTests.RenderForProvider(statement, provider);

    private static ImmutableArray<(string Name, decimal Price)> ReadProducts(
        DbConnection connection,
        string sql
    )
    {
        var result = connection.Query<(string Name, decimal Price)>(
            sql: sql,
            mapper: reader =>
                (
                    reader.GetString(0),
                    Convert.ToDecimal(reader.GetValue(1), CultureInfo.InvariantCulture)
                )
        );
        return Assert.IsType<ProductRowsOk>(result).Value.ToImmutableArray();
    }

    private static ImmutableArray<string> ReadNames(DbConnection connection, string sql)
    {
        var result = connection.Query<string>(sql: sql, mapper: reader => reader.GetString(0));
        return Assert.IsType<NamesOk>(result).Value.ToImmutableArray();
    }

    private static void CreateProductSchema(string provider, string connectionString)
    {
        var schema = provider == "postgres" ? "public" : "dbo";
        var yaml = $$"""
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
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, yaml);
            Assert.Equal(
                0,
                DataProviderMigrate.Program.Main([
                    "migrate",
                    "--schema",
                    path,
                    "--provider",
                    provider,
                    "--output",
                    connectionString,
                ])
            );
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static Guid ProductId(int index) =>
        Guid.Parse($"00000000-0000-0000-0000-{index + 1:000000000000}");

    private static void SeedPostgres(NpgsqlConnection connection)
    {
        using var importer = connection.BeginBinaryImport(
            "COPY products (id, name, price, category) FROM STDIN (FORMAT BINARY)"
        );
        for (var index = 0; index < Products.Length; index++)
        {
            var product = Products[index];
            importer.StartRow();
            importer.Write(ProductId(index), NpgsqlDbType.Uuid);
            importer.Write(product.Name, NpgsqlDbType.Text);
            importer.Write(product.Price, NpgsqlDbType.Numeric);
            importer.Write(product.Category, NpgsqlDbType.Text);
        }
        Assert.Equal(Products.Length, Convert.ToInt32(importer.Complete()));
    }

    private static void SeedSqlServer(SqlConnection connection)
    {
        using var table = new DataTable();
        table.Columns.Add("id", typeof(Guid));
        table.Columns.Add("name", typeof(string));
        table.Columns.Add("price", typeof(decimal));
        table.Columns.Add("category", typeof(string));
        for (var index = 0; index < Products.Length; index++)
        {
            var product = Products[index];
            table.Rows.Add(ProductId(index), product.Name, product.Price, product.Category);
        }
        using var bulk = new SqlBulkCopy(connection) { DestinationTableName = "dbo.products" };
        bulk.WriteToServer(table);
    }
}
