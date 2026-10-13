using System.Collections.Immutable;

namespace Nimblesite.DataProvider.Migration.SqlServer;

internal sealed record TableRow(string Schema, string Table);

internal sealed record ColumnRow(string Schema, string Table, ColumnDefinition Column);

internal sealed record KeyRow(
    string Schema,
    string Table,
    string Name,
    bool IsPrimary,
    string Column
);

internal sealed record ForeignKeyRow(
    string Schema,
    string Table,
    string Name,
    string Column,
    string ReferencedSchema,
    string ReferencedTable,
    string ReferencedColumn,
    ForeignKeyAction OnDelete,
    ForeignKeyAction OnUpdate
);

internal sealed record IndexRow(
    string Schema,
    string Table,
    string Name,
    bool IsUnique,
    string? Filter,
    string Column
);

internal sealed record CheckRow(
    string Schema,
    string Table,
    string Name,
    string? Column,
    string Expression
);

/// <summary>
/// SQL Server catalog queries used by <see cref="SqlServerSchemaInspector"/>.
/// Implements [MIG-SQLSERVER].
/// </summary>
internal static partial class SqlServerCatalogQueries
{
    internal static ImmutableArray<TableRow> Tables(SqlConnection connection) =>
        Read(
            connection,
            global::Nimblesite
                .DataProvider
                .Migration
                .SqlServer
                .SqlServerCatalogQueries
                .SqlSpecification1,
            r => new TableRow(r.GetString(0), r.GetString(1))
        );

    internal static ImmutableArray<ColumnRow> Columns(SqlConnection connection) =>
        Read(
            connection,
            global::Nimblesite
                .DataProvider
                .Migration
                .SqlServer
                .SqlServerCatalogQueries
                .SqlSpecification2,
            r => new ColumnRow(r.GetString(0), r.GetString(1), ReadColumn(r))
        );

    private static ColumnDefinition ReadColumn(SqlDataReader r) =>
        new()
        {
            Name = r.GetString(2),
            Type = SqlServerTypeMapper.FromSqlServer(
                dataType: r.GetString(3),
                maxLength: NullableInt(r, 4),
                precision: NullableInt(r, 5),
                scale: NullableInt(r, 6)
            ),
            IsNullable = r.GetString(7) == "YES",
            DefaultValue = r.IsDBNull(8)
                ? null
                : SqlExpressionText.StripOuterParens(r.GetString(8)),
            IsIdentity = NullableInt(r, 9) == 1,
        };

    internal static ImmutableArray<KeyRow> Keys(SqlConnection connection) =>
        Read(
            connection,
            global::Nimblesite
                .DataProvider
                .Migration
                .SqlServer
                .SqlServerCatalogQueries
                .SqlSpecification3,
            r => new KeyRow(
                r.GetString(0),
                r.GetString(1),
                r.GetString(2),
                r.GetString(3) == "PRIMARY KEY",
                r.GetString(4)
            )
        );

    internal static ImmutableArray<ForeignKeyRow> ForeignKeys(SqlConnection connection) =>
        Read(
            connection,
            global::Nimblesite
                .DataProvider
                .Migration
                .SqlServer
                .SqlServerCatalogQueries
                .SqlSpecification4,
            r => new ForeignKeyRow(
                r.GetString(0),
                r.GetString(1),
                r.GetString(2),
                r.GetString(3),
                r.GetString(4),
                r.GetString(5),
                r.GetString(6),
                ReferentialAction(r.GetString(7)),
                ReferentialAction(r.GetString(8))
            )
        );

    internal static ImmutableArray<IndexRow> Indexes(SqlConnection connection) =>
        Read(
            connection,
            global::Nimblesite
                .DataProvider
                .Migration
                .SqlServer
                .SqlServerCatalogQueries
                .SqlSpecification5,
            r => new IndexRow(
                r.GetString(0),
                r.GetString(1),
                r.GetString(2),
                r.GetBoolean(3),
                r.IsDBNull(4) ? null : SqlExpressionText.StripOuterParens(r.GetString(4)),
                r.GetString(5)
            )
        );

    internal static ImmutableArray<CheckRow> Checks(SqlConnection connection) =>
        Read(
            connection,
            global::Nimblesite
                .DataProvider
                .Migration
                .SqlServer
                .SqlServerCatalogQueries
                .SqlSpecification6,
            r => new CheckRow(
                r.GetString(0),
                r.GetString(1),
                r.GetString(2),
                r.IsDBNull(3) ? null : r.GetString(3),
                SqlServerExpressionNormalizer.Normalize(expression: r.GetString(4))
            )
        );

    private static ImmutableArray<T> Read<T>(
        SqlConnection connection,
        string sql,
        Func<SqlDataReader, T> map
    )
    {
        using var command = new SqlCommand(sql, connection);
        using var reader = command.ExecuteReader();
        var rows = ImmutableArray.CreateBuilder<T>();
        while (reader.Read())
        {
            rows.Add(map(reader));
        }
        return rows.ToImmutable();
    }

    private static int? NullableInt(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal)
            ? null
            : Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    private static ForeignKeyAction ReferentialAction(string description) =>
        description switch
        {
            "CASCADE" => ForeignKeyAction.Cascade,
            "SET_NULL" => ForeignKeyAction.SetNull,
            "SET_DEFAULT" => ForeignKeyAction.SetDefault,
            _ => ForeignKeyAction.NoAction,
        };
}
