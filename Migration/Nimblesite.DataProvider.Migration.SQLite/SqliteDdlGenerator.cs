using System.Globalization;

namespace Nimblesite.DataProvider.Migration.SQLite;

/// <summary>
/// SQLite DDL generator for schema operations.
/// </summary>
public static class SqliteDdlGenerator
{
    /// <summary>
    /// Generate SQLite DDL for a schema operation.
    /// </summary>
    public static string Generate(SchemaOperation operation) =>
        operation switch
        {
            CreateTableOperation op => GenerateCreateTable(op.Table),
            AddColumnOperation op => GenerateAddColumn(op),
            CreateIndexOperation op => GenerateCreateIndex(op),
            AddForeignKeyOperation => throw new NotSupportedException(
                "SQLite does not support adding foreign keys to existing tables. Recreate the table instead."
            ),
            AddCheckConstraintOperation => throw new NotSupportedException(
                "SQLite does not support adding check constraints to existing tables. Recreate the table instead."
            ),
            AddUniqueConstraintOperation => throw new NotSupportedException(
                "SQLite does not support adding unique constraints to existing tables. Use CREATE UNIQUE INDEX instead."
            ),
            DropTableOperation op => $"DROP TABLE IF EXISTS [{op.TableName}]",
            DropColumnOperation => throw new NotSupportedException(
                "SQLite does not support DROP COLUMN. Recreate the table instead."
            ),
            DropIndexOperation op => $"DROP INDEX IF EXISTS [{op.IndexName}]",
            DropForeignKeyOperation => throw new NotSupportedException(
                "SQLite does not support dropping foreign keys. Recreate the table instead."
            ),
            EnableRlsOperation => SqliteRlsDdlBuilder.GenerateEnable(),
            CreateRlsPolicyOperation op => SqliteRlsDdlBuilder.GenerateCreatePolicy(op),
            DropRlsPolicyOperation op => SqliteRlsDdlBuilder.GenerateDropPolicy(op),
            DisableRlsOperation op => SqliteRlsDdlBuilder.GenerateDisable(op),
            _ => throw new NotSupportedException(
                $"Unknown operation type: {operation.GetType().Name}"
            ),
        };

    private static string GenerateCreateTable(TableDefinition table)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"CREATE TABLE IF NOT EXISTS [{table.Name}] (");

        var columnDefs = new List<string>();

        foreach (var column in table.Columns)
        {
            columnDefs.Add(GenerateColumnDef(column));
        }

        // Add primary key constraint if specified
        if (table.PrimaryKey is not null && table.PrimaryKey.Columns.Count > 0)
        {
            var pkCols = string.Join(", ", table.PrimaryKey.Columns.Select(c => $"[{c}]"));
            columnDefs.Add($"PRIMARY KEY ({pkCols})");
        }

        // Add foreign key constraints
        foreach (var fk in table.ForeignKeys)
        {
            var fkCols = string.Join(", ", fk.Columns.Select(c => $"[{c}]"));
            var refCols = string.Join(", ", fk.ReferencedColumns.Select(c => $"[{c}]"));
            var onDelete = ForeignKeyActionToSql(fk.OnDelete);
            var onUpdate = ForeignKeyActionToSql(fk.OnUpdate);

            columnDefs.Add(
                $"FOREIGN KEY ({fkCols}) REFERENCES [{fk.ReferencedTable}] ({refCols}) ON DELETE {onDelete} ON UPDATE {onUpdate}"
            );
        }

        // Add unique constraints
        foreach (var uc in table.UniqueConstraints)
        {
            var ucCols = string.Join(", ", uc.Columns.Select(c => $"[{c}]"));
            columnDefs.Add($"UNIQUE ({ucCols})");
        }

        // Add check constraints
        foreach (var cc in table.CheckConstraints)
        {
            columnDefs.Add($"CHECK ({cc.Expression})");
        }

        sb.Append(string.Join(", ", columnDefs));
        sb.Append(')');

        // Generate CREATE INDEX statements for any indexes
        foreach (var index in table.Indexes)
        {
            sb.AppendLine(";");
            var unique = index.IsUnique ? "UNIQUE " : "";
            // Expression indexes use Expressions verbatim, column indexes quote column names
            var indexItems =
                index.Expressions.Count > 0
                    ? string.Join(", ", index.Expressions)
                    : string.Join(", ", index.Columns.Select(c => $"[{c}]"));
            var filter = index.Filter is not null ? $" WHERE {index.Filter}" : "";
            sb.Append(
                CultureInfo.InvariantCulture,
                $"CREATE {unique}INDEX IF NOT EXISTS [{index.Name}] ON [{table.Name}] ({indexItems}){filter}"
            );
        }

        return sb.ToString();
    }

    private static string GenerateColumnDef(ColumnDefinition column)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"[{column.Name}] ");
        sb.Append(PortableTypeToSqlite(column.Type));

        if (!column.IsNullable)
        {
            sb.Append(" NOT NULL");
        }

        // LQL expression takes precedence over raw SQL default
        if (column.DefaultLqlExpression is not null)
        {
            var translated = LqlDefaultTranslator.ToSqlite(column.DefaultLqlExpression);
            sb.Append(CultureInfo.InvariantCulture, $" DEFAULT {translated}");
        }
        else if (column.DefaultValue is not null)
        {
            sb.Append(CultureInfo.InvariantCulture, $" DEFAULT {column.DefaultValue}");
        }

        if (column.Collation is not null)
        {
            sb.Append(CultureInfo.InvariantCulture, $" COLLATE {column.Collation}");
        }

        if (column.CheckConstraint is not null)
        {
            sb.Append(CultureInfo.InvariantCulture, $" CHECK ({column.CheckConstraint})");
        }

        return sb.ToString();
    }

    private static string GenerateAddColumn(AddColumnOperation op)
    {
        var colDef = GenerateColumnDef(op.Column);
        return $"ALTER TABLE [{op.TableName}] ADD COLUMN {colDef}";
    }

    private static string GenerateCreateIndex(CreateIndexOperation op)
    {
        var unique = op.Index.IsUnique ? "UNIQUE " : "";
        // Expression indexes use Expressions verbatim, column indexes quote column names
        var indexItems =
            op.Index.Expressions.Count > 0
                ? string.Join(", ", op.Index.Expressions)
                : string.Join(", ", op.Index.Columns.Select(c => $"[{c}]"));
        var filter = op.Index.Filter is not null ? $" WHERE {op.Index.Filter}" : "";

        return $"CREATE {unique}INDEX IF NOT EXISTS [{op.Index.Name}] ON [{op.TableName}] ({indexItems}){filter}";
    }

    /// <summary>
    /// Map portable type to SQLite type affinity.
    /// </summary>
    public static string PortableTypeToSqlite(PortableType type) =>
        type switch
        {
            // Integer types -> INTEGER
            TinyIntType or SmallIntType or IntType or BigIntType => "INTEGER",
            BooleanType => "BOOLEAN",

            // Decimal/float types -> REAL
            DecimalType or MoneyType or SmallMoneyType => "REAL",
            FloatType or DoubleType => "REAL",

            // String types -> TEXT
            CharType or VarCharType or NCharType or NVarCharType or TextType => "TEXT",
            JsonType or XmlType => "TEXT",
            EnumType => "TEXT",

            // Binary types -> BLOB
            BinaryType or VarBinaryType or BlobType => "BLOB",
            RowVersionType => "BLOB",
            GeometryType or GeographyType => "BLOB",
            // SQLite has no native vector type; embeddings fall back to BLOB.
            VectorType => "BLOB",

            // Date/time types -> TEXT (ISO 8601)
            DateType or TimeType or DateTimeType or DateTimeOffsetType => "TEXT",

            // UUID -> TEXT
            UuidType => "TEXT",

            _ => "TEXT",
        };

    private static string ForeignKeyActionToSql(ForeignKeyAction action) =>
        action switch
        {
            ForeignKeyAction.NoAction => "NO ACTION",
            ForeignKeyAction.Cascade => "CASCADE",
            ForeignKeyAction.SetNull => "SET NULL",
            ForeignKeyAction.SetDefault => "SET DEFAULT",
            ForeignKeyAction.Restrict => "RESTRICT",
            _ => "NO ACTION",
        };
}
