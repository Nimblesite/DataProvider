namespace Nimblesite.DataProvider.Migration.SqlServer;

/// <summary>
/// SQL Server DDL generator for schema operations. Constraint names default to
/// the same PK_/UQ_/FK_ names the integrity verifier expects.
/// Implements [MIG-SQLSERVER].
/// </summary>
public static class SqlServerDdlGenerator
{
    /// <summary>
    /// Generate SQL Server DDL for a schema operation.
    /// </summary>
    public static string Generate(SchemaOperation operation) =>
        operation switch
        {
            CreateTableOperation op => GenerateCreateTable(op.Table),
            AddColumnOperation op =>
                $"ALTER TABLE {Table(op.Schema, op.TableName)} ADD {ColumnDef(op.Column)}",
            MakeColumnNullableOperation op => MakeNullable(op),
            CreateIndexOperation op => GenerateCreateIndex(op.Schema, op.TableName, op.Index),
            AddForeignKeyOperation op =>
                $"ALTER TABLE {Table(op.Schema, op.TableName)} ADD {ForeignKey(op.TableName, op.ForeignKey)}",
            AddCheckConstraintOperation op =>
                $"ALTER TABLE {Table(op.Schema, op.TableName)} ADD {Check(op.CheckConstraint)}",
            AddUniqueConstraintOperation op =>
                $"ALTER TABLE {Table(op.Schema, op.TableName)} ADD {Unique(op.TableName, op.UniqueConstraint)}",
            _ => GenerateDrop(operation),
        };

    private static string GenerateDrop(SchemaOperation operation) =>
        operation switch
        {
            DropTableOperation op => $"DROP TABLE IF EXISTS {Table(op.Schema, op.TableName)}",
            DropColumnOperation op =>
                $"ALTER TABLE {Table(op.Schema, op.TableName)} DROP COLUMN {Quote(op.ColumnName)}",
            DropIndexOperation op =>
                $"DROP INDEX IF EXISTS {Quote(op.IndexName)} ON {Table(op.Schema, op.TableName)}",
            DropForeignKeyOperation op => DropConstraint(
                op.Schema,
                op.TableName,
                op.ConstraintName
            ),
            DropCheckConstraintOperation op => DropConstraint(
                op.Schema,
                op.TableName,
                op.ConstraintName
            ),
            _ => throw new NotSupportedException(
                $"SQL Server migration does not support {operation.GetType().Name}"
            ),
        };

    // ALTER COLUMN must restate the type, which the operation does not carry,
    // so read the live type and collation from the catalog and run it dynamically.
    private static string MakeNullable(MakeColumnNullableOperation op)
    {
        var table = Table(op.Schema, op.TableName);
        return $$"""
            DECLARE @type nvarchar(300) = (
                SELECT CASE
                    WHEN t.name IN ('varchar', 'char', 'varbinary', 'binary')
                        THEN t.name + '(' + IIF(c.max_length = -1, 'MAX', CAST(c.max_length AS nvarchar(10))) + ')'
                    WHEN t.name IN ('nvarchar', 'nchar')
                        THEN t.name + '(' + IIF(c.max_length = -1, 'MAX', CAST(c.max_length / 2 AS nvarchar(10))) + ')'
                    WHEN t.name IN ('decimal', 'numeric')
                        THEN t.name + '(' + CAST(c.precision AS nvarchar(10)) + ',' + CAST(c.scale AS nvarchar(10)) + ')'
                    WHEN t.name IN ('datetime2', 'time', 'datetimeoffset')
                        THEN t.name + '(' + CAST(c.scale AS nvarchar(10)) + ')'
                    ELSE t.name END
                    + IIF(c.collation_name IS NULL, '', ' COLLATE ' + c.collation_name)
                FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id
                WHERE c.object_id = OBJECT_ID({{Literal(table)}}) AND c.name = {{Literal(
                op.ColumnName
            )}});
            EXEC({{Literal(
                $"ALTER TABLE {table} ALTER COLUMN {Quote(op.ColumnName)} "
            )}} + @type + N' NULL');
            """;
    }

    private static string Literal(string value) =>
        $"N'{value.Replace("'", "''", StringComparison.Ordinal)}'";

    private static string GenerateCreateTable(TableDefinition table)
    {
        var definitions = table
            .Columns.Select(column =>
                ColumnDef(
                    PrimaryKeyNullability.UsesPlatformDefault(table, column)
                        ? column with
                        {
                            IsNullable = false,
                        }
                        : column
                )
            )
            .Concat(PrimaryKey(table))
            .Concat(table.ForeignKeys.Select(fk => ForeignKey(table.Name, fk)))
            .Concat(table.UniqueConstraints.Select(uc => Unique(table.Name, uc)))
            .Concat(table.CheckConstraints.Select(Check));
        var sb = new StringBuilder();
        sb.Append(
            CultureInfo.InvariantCulture,
            $"CREATE TABLE {Table(table.Schema, table.Name)} ({string.Join(", ", definitions)})"
        );
        foreach (var index in table.Indexes)
        {
            sb.Append(";\n").Append(GenerateCreateIndex(table.Schema, table.Name, index));
        }
        return sb.ToString();
    }

    private static IEnumerable<string> PrimaryKey(TableDefinition table) =>
        table.PrimaryKey is { Columns.Count: > 0 } pk
            ?
            [
                $"CONSTRAINT {Quote(pk.Name ?? $"PK_{table.Name}")} PRIMARY KEY ({Columns(pk.Columns)})",
            ]
            : [];

    private static string ColumnDef(ColumnDefinition column) =>
        column.ComputedExpression is not null
            ? $"{Quote(column.Name)} AS ({column.ComputedExpression}){(column.IsComputedPersisted ? " PERSISTED" : "")}"
            : $"{Quote(column.Name)} {SqlServerTypeMapper.ToSqlServer(column.Type)}{ColumnSuffix(column)}";

    private static string ColumnSuffix(ColumnDefinition column)
    {
        var sb = new StringBuilder();
        if (column.IsIdentity)
        {
            sb.Append(
                CultureInfo.InvariantCulture,
                $" IDENTITY({column.IdentitySeed},{column.IdentityIncrement})"
            );
        }
        sb.Append(column.IsNullable ? " NULL" : " NOT NULL");
        sb.Append(Default(column));
        sb.Append(column.Collation is null ? "" : $" COLLATE {column.Collation}");
        sb.Append(ColumnCheck(column));
        return sb.ToString();
    }

    private static string Default(ColumnDefinition column) =>
        column switch
        {
            { DefaultLqlExpression: not null } => throw new NotSupportedException(
                $"SQL Server migration does not support LQL default on column {column.Name}"
            ),
            { DefaultValue: not null } => $" DEFAULT {column.DefaultValue}",
            _ => "",
        };

    private static string ColumnCheck(ColumnDefinition column) =>
        column.CheckConstraint is null ? ""
        : column.CheckConstraintName is null ? $" CHECK ({column.CheckConstraint})"
        : $" CONSTRAINT {Quote(column.CheckConstraintName)} CHECK ({column.CheckConstraint})";

    private static string GenerateCreateIndex(
        string schema,
        string tableName,
        IndexDefinition index
    )
    {
        if (index.Expressions.Count > 0)
        {
            throw new NotSupportedException(
                $"SQL Server does not support expression index {index.Name}; use a computed column"
            );
        }
        var unique = index.IsUnique ? "UNIQUE " : "";
        var filter = index.Filter is null ? "" : $" WHERE {index.Filter}";
        return $"CREATE {unique}INDEX {Quote(index.Name)} ON {Table(schema, tableName)} ({Columns(index.Columns)}){filter}";
    }

    private static string ForeignKey(string tableName, ForeignKeyDefinition fk) =>
        $"CONSTRAINT {Quote(fk.Name ?? $"FK_{tableName}_{string.Join("_", fk.Columns)}")} "
        + $"FOREIGN KEY ({Columns(fk.Columns)}) "
        + $"REFERENCES {Table(fk.ReferencedSchema, fk.ReferencedTable)} ({Columns(fk.ReferencedColumns)}) "
        + $"ON DELETE {ForeignKeyAction(fk.OnDelete)} ON UPDATE {ForeignKeyAction(fk.OnUpdate)}";

    private static string Unique(string tableName, UniqueConstraintDefinition uc) =>
        $"CONSTRAINT {Quote(uc.Name ?? $"UQ_{tableName}_{string.Join("_", uc.Columns)}")} UNIQUE ({Columns(uc.Columns)})";

    private static string Check(CheckConstraintDefinition cc) =>
        $"CONSTRAINT {Quote(cc.Name)} CHECK ({cc.Expression})";

    private static string DropConstraint(string schema, string tableName, string name) =>
        $"ALTER TABLE {Table(schema, tableName)} DROP CONSTRAINT {Quote(name)}";

    private static string ForeignKeyAction(ForeignKeyAction action) =>
        action switch
        {
            Core.ForeignKeyAction.NoAction => "NO ACTION",
            Core.ForeignKeyAction.Cascade => "CASCADE",
            Core.ForeignKeyAction.SetNull => "SET NULL",
            Core.ForeignKeyAction.SetDefault => "SET DEFAULT",
            _ => throw new NotSupportedException(
                $"SQL Server does not support foreign key action {action}; use NoAction"
            ),
        };

    private static string Columns(IEnumerable<string> columns) =>
        string.Join(", ", columns.Select(Quote));

    private static string Table(string schema, string name) =>
        $"{Quote(SqlServerSchemaInspector.EffectiveSchema(schema))}.{Quote(name)}";

    private static string Quote(string identifier) =>
        $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";
}
