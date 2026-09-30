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
internal static class SqlServerCatalogQueries
{
    internal static ImmutableArray<TableRow> Tables(SqlConnection connection) =>
        Read(
            connection,
            """
            SELECT TABLE_SCHEMA, TABLE_NAME FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_SCHEMA, TABLE_NAME
            """,
            r => new TableRow(r.GetString(0), r.GetString(1))
        );

    internal static ImmutableArray<ColumnRow> Columns(SqlConnection connection) =>
        Read(
            connection,
            """
            SELECT c.TABLE_SCHEMA, c.TABLE_NAME, c.COLUMN_NAME, c.DATA_TYPE,
                   c.CHARACTER_MAXIMUM_LENGTH,
                   COALESCE(CAST(c.NUMERIC_PRECISION AS INT), CAST(c.DATETIME_PRECISION AS INT)),
                   c.NUMERIC_SCALE, c.IS_NULLABLE, c.COLUMN_DEFAULT,
                   COLUMNPROPERTY(OBJECT_ID(QUOTENAME(c.TABLE_SCHEMA) + '.' + QUOTENAME(c.TABLE_NAME)),
                                  c.COLUMN_NAME, 'IsIdentity')
            FROM INFORMATION_SCHEMA.COLUMNS c
            JOIN INFORMATION_SCHEMA.TABLES t
              ON t.TABLE_SCHEMA = c.TABLE_SCHEMA AND t.TABLE_NAME = c.TABLE_NAME
            WHERE t.TABLE_TYPE = 'BASE TABLE'
            ORDER BY c.TABLE_SCHEMA, c.TABLE_NAME, c.ORDINAL_POSITION
            """,
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
            """
            SELECT tc.TABLE_SCHEMA, tc.TABLE_NAME, tc.CONSTRAINT_NAME, tc.CONSTRAINT_TYPE, k.COLUMN_NAME
            FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE k
              ON k.CONSTRAINT_SCHEMA = tc.CONSTRAINT_SCHEMA AND k.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
            WHERE tc.CONSTRAINT_TYPE IN ('PRIMARY KEY', 'UNIQUE')
            ORDER BY tc.TABLE_SCHEMA, tc.TABLE_NAME, tc.CONSTRAINT_NAME, k.ORDINAL_POSITION
            """,
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
            """
            SELECT SCHEMA_NAME(fk.schema_id), OBJECT_NAME(fk.parent_object_id), fk.name,
                   COL_NAME(fkc.parent_object_id, fkc.parent_column_id),
                   SCHEMA_NAME(rt.schema_id), rt.name,
                   COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id),
                   fk.delete_referential_action_desc, fk.update_referential_action_desc
            FROM sys.foreign_keys fk
            JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
            JOIN sys.tables rt ON rt.object_id = fk.referenced_object_id
            ORDER BY 1, 2, 3, fkc.constraint_column_id
            """,
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
            """
            SELECT SCHEMA_NAME(t.schema_id), t.name, i.name, i.is_unique, i.filter_definition,
                   COL_NAME(ic.object_id, ic.column_id)
            FROM sys.indexes i
            JOIN sys.tables t ON t.object_id = i.object_id
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            WHERE i.is_primary_key = 0 AND i.is_unique_constraint = 0 AND i.type > 0
              AND ic.is_included_column = 0
            ORDER BY 1, 2, 3, ic.key_ordinal
            """,
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
            """
            SELECT SCHEMA_NAME(t.schema_id), t.name, cc.name,
                   COL_NAME(cc.parent_object_id, NULLIF(cc.parent_column_id, 0)), cc.definition
            FROM sys.check_constraints cc
            JOIN sys.tables t ON t.object_id = cc.parent_object_id
            ORDER BY 1, 2, 3
            """,
            r => new CheckRow(
                r.GetString(0),
                r.GetString(1),
                r.GetString(2),
                r.IsDBNull(3) ? null : r.GetString(3),
                SqlExpressionText.StripOuterParens(r.GetString(4))
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
