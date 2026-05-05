using System.Globalization;

namespace Nimblesite.DataProvider.Migration.Postgres;

/// <summary>
/// Result of a schema migration operation.
/// </summary>
/// <param name="Success">Whether the migration completed without errors.</param>
/// <param name="TablesCreated">Number of tables successfully created or already existing.</param>
/// <param name="Errors">List of table names and error messages for any failures.</param>
public sealed record MigrationResult(bool Success, int TablesCreated, IReadOnlyList<string> Errors);

/// <summary>
/// PostgreSQL DDL generator for schema operations.
/// </summary>
public static partial class PostgresDdlGenerator
{
    /// <summary>
    /// Migrate a schema definition to PostgreSQL, creating all tables.
    /// Each table is created independently - failures on one table don't block others.
    /// Uses CREATE TABLE IF NOT EXISTS for idempotency.
    /// </summary>
    /// <param name="connection">Open database connection.</param>
    /// <param name="schema">Schema definition to migrate.</param>
    /// <param name="onTableCreated">Optional callback for each table created (table name).</param>
    /// <param name="onTableFailed">Optional callback for each table that failed (table name, exception).</param>
    /// <returns>Migration result with success status and any errors.</returns>
    public static MigrationResult MigrateSchema(
        IDbConnection connection,
        SchemaDefinition schema,
        Action<string>? onTableCreated = null,
        Action<string, Exception>? onTableFailed = null
    )
    {
        var errors = new List<string>();
        var tablesCreated = 0;

        // [MIG-TYPES-VECTOR] §5.4.2: if any table in this schema has a VectorType
        // column, run CREATE EXTENSION IF NOT EXISTS vector exactly once before
        // emitting table DDL. pgvector is installed per-database; repeated calls
        // are a no-op, but the prelude is mandatory — without it the subsequent
        // CREATE TABLE fails with "type vector does not exist".
        var hasVector = schema.Tables.Any(t => t.Columns.Any(c => c.Type is VectorType));
        if (hasVector)
        {
            try
            {
                using var extCmd = connection.CreateCommand();
                extCmd.CommandText = "CREATE EXTENSION IF NOT EXISTS vector";
                extCmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                // MIG-E-VECTOR-PG-EXT-PERM: surface the failure as a schema-level
                // error; every table that would use vector is doomed otherwise.
                // Include the full exception (type + message + inner) so the
                // caller can diagnose permission issues vs missing pgvector.
                errors.Add(
                    $"MIG-E-VECTOR-PG-EXT-PERM: {ex.GetType().Name}: {ex.Message}"
                        + (ex.InnerException is null ? "" : $" -> {ex.InnerException}")
                );
                onTableFailed?.Invoke("__pgvector_extension__", ex);
                return new MigrationResult(
                    Success: false,
                    TablesCreated: 0,
                    Errors: errors.AsReadOnly()
                );
            }
        }

        foreach (var table in schema.Tables)
        {
            try
            {
                var ddl = Generate(new CreateTableOperation(table));
                using var cmd = connection.CreateCommand();
                cmd.CommandText = ddl;
                cmd.ExecuteNonQuery();
                tablesCreated++;
                onTableCreated?.Invoke(table.Name);
            }
            catch (Exception ex)
            {
                errors.Add($"{table.Name}: {ex.Message}");
                onTableFailed?.Invoke(table.Name, ex);
            }
        }

        return new MigrationResult(
            Success: errors.Count == 0,
            TablesCreated: tablesCreated,
            Errors: errors.AsReadOnly()
        );
    }

    /// <summary>
    /// Generate PostgreSQL DDL for a schema operation.
    /// </summary>
    public static string Generate(SchemaOperation operation) =>
        operation switch
        {
            CreateTableOperation op => GenerateCreateTable(op.Table),
            AddColumnOperation op => GenerateAddColumn(op),
            CreateIndexOperation op => GenerateCreateIndex(op),
            AddForeignKeyOperation op => GenerateAddForeignKey(op),
            AddCheckConstraintOperation op => GenerateAddCheckConstraint(op),
            AddUniqueConstraintOperation op => GenerateAddUniqueConstraint(op),
            CreateOrAlterRoleOperation op => GenerateCreateOrAlterRole(op),
            CreateOrReplaceFunctionOperation op => GenerateCreateOrReplaceFunction(op),
            GrantPrivilegesOperation op => GenerateGrantPrivileges(op.Grant),
            DropTableOperation op =>
                $"DROP TABLE IF EXISTS \"{op.Schema}\".\"{op.TableName}\" CASCADE",
            DropColumnOperation op =>
                $"ALTER TABLE \"{op.Schema}\".\"{op.TableName}\" DROP COLUMN \"{op.ColumnName}\"",
            DropIndexOperation op => $"DROP INDEX IF EXISTS \"{op.Schema}\".\"{op.IndexName}\"",
            DropForeignKeyOperation op =>
                $"ALTER TABLE \"{op.Schema}\".\"{op.TableName}\" DROP CONSTRAINT \"{op.ConstraintName}\"",
            DropFunctionOperation op => GenerateDropFunction(op),
            RevokePrivilegesOperation op => GenerateRevokePrivileges(op.Grant),
            EnableRlsOperation op =>
                $"ALTER TABLE \"{op.Schema}\".\"{op.TableName}\" ENABLE ROW LEVEL SECURITY",
            DisableRlsOperation op =>
                $"ALTER TABLE \"{op.Schema}\".\"{op.TableName}\" DISABLE ROW LEVEL SECURITY",
            EnableForceRlsOperation op =>
                $"ALTER TABLE \"{op.Schema}\".\"{op.TableName}\" FORCE ROW LEVEL SECURITY",
            DisableForceRlsOperation op =>
                $"ALTER TABLE \"{op.Schema}\".\"{op.TableName}\" NO FORCE ROW LEVEL SECURITY",
            DropRlsPolicyOperation op =>
                $"DROP POLICY IF EXISTS \"{op.PolicyName}\" ON \"{op.Schema}\".\"{op.TableName}\"",
            CreateRlsPolicyOperation op => GenerateCreateRlsPolicy(op),
            _ => throw new NotSupportedException(
                $"Unknown operation type: {operation.GetType().Name}"
            ),
        };

    private static string GenerateCreateRlsPolicy(CreateRlsPolicyOperation op)
    {
        // Implements [RLS-PG]. Transpiles LQL predicates to PostgreSQL using
        // RlsPredicateTranspiler and emits a CREATE POLICY statement.
        ValidatePolicyPredicates(op.Policy);

        var sb = new StringBuilder();
        sb.Append(
            CultureInfo.InvariantCulture,
            $"CREATE POLICY \"{op.Policy.Name}\" ON \"{op.Schema}\".\"{op.TableName}\""
        );
        sb.Append(op.Policy.IsPermissive ? " AS PERMISSIVE" : " AS RESTRICTIVE");
        sb.Append(
            CultureInfo.InvariantCulture,
            $" FOR {RlsOperationsToPgClause(op.Policy.Operations)}"
        );
        sb.Append(CultureInfo.InvariantCulture, $" TO {RlsRolesToPgClause(op.Policy.Roles)}");

        // Raw-SQL escape hatch (issue #36) takes precedence over LQL.
        if (!string.IsNullOrWhiteSpace(op.Policy.UsingSql))
        {
            sb.Append(CultureInfo.InvariantCulture, $" USING ({op.Policy.UsingSql})");
        }
        else if (!string.IsNullOrWhiteSpace(op.Policy.UsingLql))
        {
            var sql = TranslateOrThrow(op.Policy.UsingLql, op.Policy.Name);
            sb.Append(CultureInfo.InvariantCulture, $" USING ({sql})");
        }

        if (!string.IsNullOrWhiteSpace(op.Policy.WithCheckSql))
        {
            sb.Append(CultureInfo.InvariantCulture, $" WITH CHECK ({op.Policy.WithCheckSql})");
        }
        else if (!string.IsNullOrWhiteSpace(op.Policy.WithCheckLql))
        {
            var sql = TranslateOrThrow(op.Policy.WithCheckLql, op.Policy.Name);
            sb.Append(CultureInfo.InvariantCulture, $" WITH CHECK ({sql})");
        }
        return sb.ToString();
    }

    private static void ValidatePolicyPredicates(RlsPolicyDefinition policy)
    {
        var needsUsing = policy.Operations.Any(o =>
            o
                is RlsOperation.All
                    or RlsOperation.Select
                    or RlsOperation.Update
                    or RlsOperation.Delete
        );
        var hasUsing =
            !string.IsNullOrWhiteSpace(policy.UsingLql)
            || !string.IsNullOrWhiteSpace(policy.UsingSql);
        if (needsUsing && !hasUsing)
        {
            throw new InvalidOperationException(
                MigrationError.RlsEmptyPredicate(policy.Name).Message
            );
        }

        var needsWithCheck = policy.Operations.Any(o =>
            o is RlsOperation.All or RlsOperation.Insert or RlsOperation.Update
        );
        var hasWithCheck =
            !string.IsNullOrWhiteSpace(policy.WithCheckLql)
            || !string.IsNullOrWhiteSpace(policy.WithCheckSql);
        if (needsWithCheck && !hasWithCheck)
        {
            throw new InvalidOperationException(MigrationError.RlsEmptyCheck(policy.Name).Message);
        }
    }

    private static string TranslateOrThrow(string lql, string policyName)
    {
        var result = RlsPredicateTranspiler.Translate(lql, RlsPlatform.Postgres, policyName);
        return result switch
        {
            Outcome.Result<string, MigrationError>.Ok<string, MigrationError> ok => ok.Value,
            Outcome.Result<string, MigrationError>.Error<string, MigrationError> err =>
                throw new InvalidOperationException(err.Value.Message),
        };
    }

    private static string RlsOperationsToPgClause(IReadOnlyList<RlsOperation> ops)
    {
        if (ops.Count == 0 || ops.Contains(RlsOperation.All))
        {
            return "ALL";
        }
        // Postgres FOR clause supports a single operation. Multiple require
        // multiple CREATE POLICY statements. For v1 we pick the first and
        // require callers to split policies if they need many — same behavior
        // as native CREATE POLICY semantics.
        return ops[0] switch
        {
            RlsOperation.Select => "SELECT",
            RlsOperation.Insert => "INSERT",
            RlsOperation.Update => "UPDATE",
            RlsOperation.Delete => "DELETE",
            _ => "ALL",
        };
    }

    private static string RlsRolesToPgClause(IReadOnlyList<string> roles) =>
        roles.Count == 0 ? "PUBLIC" : string.Join(", ", roles.Select(r => $"\"{r}\""));

    private static string GenerateCreateTable(TableDefinition table)
    {
        var sb = new StringBuilder();
        var tableName = table.Name;
        var schemaName = table.Schema;

        // [MIG-TYPES-VECTOR] §5.4.2: if this table contains any VectorType
        // column, the pgvector extension MUST exist in the current database
        // before the CREATE TABLE is issued, otherwise Postgres fails with
        // "type vector does not exist". Emitting the idempotent prelude
        // inline in the generated DDL means every call site gets it for
        // free — both the schema-level MigrateSchema path and the
        // operation-at-a-time MigrationRunner.Apply path used by the
        // schema-diff migration pipeline.
        if (table.Columns.Any(c => c.Type is VectorType))
        {
            sb.Append("CREATE EXTENSION IF NOT EXISTS vector; ");
        }

        sb.Append(
            CultureInfo.InvariantCulture,
            $"CREATE TABLE IF NOT EXISTS \"{schemaName}\".\"{tableName}\" ("
        );

        var columnDefs = new List<string>();

        foreach (var column in table.Columns)
        {
            columnDefs.Add(GenerateColumnDef(column));
        }

        // Add primary key constraint
        if (table.PrimaryKey is not null && table.PrimaryKey.Columns.Count > 0)
        {
            var pkName = (table.PrimaryKey.Name ?? $"PK_{table.Name}");
            var pkCols = string.Join(", ", table.PrimaryKey.Columns.Select(c => $"\"{c}\""));
            columnDefs.Add($"CONSTRAINT \"{pkName}\" PRIMARY KEY ({pkCols})");
        }

        // Add foreign key constraints
        foreach (var fk in table.ForeignKeys)
        {
            var fkName = (fk.Name ?? $"FK_{table.Name}_{string.Join("_", fk.Columns)}");
            var fkCols = string.Join(", ", fk.Columns.Select(c => $"\"{c}\""));
            var refCols = string.Join(", ", fk.ReferencedColumns.Select(c => $"\"{c}\""));
            var onDelete = ForeignKeyActionToSql(fk.OnDelete);
            var onUpdate = ForeignKeyActionToSql(fk.OnUpdate);
            var refTable = fk.ReferencedTable;
            var refSchema = fk.ReferencedSchema;

            columnDefs.Add(
                $"CONSTRAINT \"{fkName}\" FOREIGN KEY ({fkCols}) REFERENCES \"{refSchema}\".\"{refTable}\" ({refCols}) ON DELETE {onDelete} ON UPDATE {onUpdate}"
            );
        }

        // Add unique constraints
        foreach (var uc in table.UniqueConstraints)
        {
            var ucName = (uc.Name ?? $"UQ_{table.Name}_{string.Join("_", uc.Columns)}");
            var ucCols = string.Join(", ", uc.Columns.Select(c => $"\"{c}\""));
            columnDefs.Add($"CONSTRAINT \"{ucName}\" UNIQUE ({ucCols})");
        }

        // Add check constraints. Auto-quote bare identifiers in the
        // expression that match a column name on this table, so a
        // mixed-case column like "Status" survives the round-trip
        // (Postgres folds unquoted identifiers to lower case otherwise).
        var columnNames = table.Columns.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var cc in table.CheckConstraints)
        {
            var quotedExpr = QuoteIdentifiersInExpression(cc.Expression, columnNames);
            columnDefs.Add($"CONSTRAINT \"{cc.Name}\" CHECK ({quotedExpr})");
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
                    : string.Join(", ", index.Columns.Select(c => $"\"{c}\""));
            var filter = index.Filter is not null ? $" WHERE {index.Filter}" : "";
            var indexName = index.Name;
            sb.Append(
                CultureInfo.InvariantCulture,
                $"CREATE {unique}INDEX IF NOT EXISTS \"{indexName}\" ON \"{schemaName}\".\"{tableName}\" ({indexItems}){filter}"
            );
        }

        return sb.ToString();
    }

    private static string GenerateColumnDef(ColumnDefinition column)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"\"{column.Name}\" ");

        // Handle identity columns
        if (column.IsIdentity)
        {
            sb.Append(
                column.Type switch
                {
                    SmallIntType => "SMALLSERIAL",
                    IntType => "SERIAL",
                    BigIntType => "BIGSERIAL",
                    _ => "SERIAL",
                }
            );
        }
        else
        {
            sb.Append(PortableTypeToPostgres(column.Type));
        }

        if (!column.IsNullable && !column.IsIdentity)
        {
            sb.Append(" NOT NULL");
        }

        // LQL expression takes precedence over raw SQL default
        if (column.DefaultLqlExpression is not null)
        {
            var translated = LqlDefaultTranslator.ToPostgres(column.DefaultLqlExpression);
            sb.Append(CultureInfo.InvariantCulture, $" DEFAULT {translated}");
        }
        else if (column.DefaultValue is not null)
        {
            sb.Append(CultureInfo.InvariantCulture, $" DEFAULT {column.DefaultValue}");
        }

        if (column.CheckConstraint is not null)
        {
            // Auto-quote the column's own name in its CHECK expression so
            // mixed-case columns survive without manual quoting in YAML.
            var ownNames = new HashSet<string>(StringComparer.Ordinal) { column.Name };
            var quotedExpr = QuoteIdentifiersInExpression(column.CheckConstraint, ownNames);
            sb.Append(CultureInfo.InvariantCulture, $" CHECK ({quotedExpr})");
        }

        return sb.ToString();
    }

    private static string GenerateAddColumn(AddColumnOperation op)
    {
        var colDef = GenerateColumnDef(op.Column);
        return $"ALTER TABLE \"{op.Schema}\".\"{op.TableName}\" ADD COLUMN {colDef}";
    }

    private static string GenerateCreateIndex(CreateIndexOperation op)
    {
        var unique = op.Index.IsUnique ? "UNIQUE " : "";
        // Expression indexes use Expressions verbatim, column indexes quote column names
        var indexItems =
            op.Index.Expressions.Count > 0
                ? string.Join(", ", op.Index.Expressions)
                : string.Join(", ", op.Index.Columns.Select(c => $"\"{c}\""));
        var filter = op.Index.Filter is not null ? $" WHERE {op.Index.Filter}" : "";

        return $"CREATE {unique}INDEX IF NOT EXISTS \"{op.Index.Name}\" ON \"{op.Schema}\".\"{op.TableName}\" ({indexItems}){filter}";
    }

    private static string GenerateAddForeignKey(AddForeignKeyOperation op)
    {
        var fk = op.ForeignKey;
        var fkName = (fk.Name ?? $"FK_{op.TableName}_{string.Join("_", fk.Columns)}");
        var fkCols = string.Join(", ", fk.Columns.Select(c => $"\"{c}\""));
        var refCols = string.Join(", ", fk.ReferencedColumns.Select(c => $"\"{c}\""));
        var onDelete = ForeignKeyActionToSql(fk.OnDelete);
        var onUpdate = ForeignKeyActionToSql(fk.OnUpdate);

        return $"ALTER TABLE \"{op.Schema}\".\"{op.TableName}\" ADD CONSTRAINT \"{fkName}\" FOREIGN KEY ({fkCols}) REFERENCES \"{fk.ReferencedSchema}\".\"{fk.ReferencedTable}\" ({refCols}) ON DELETE {onDelete} ON UPDATE {onUpdate}";
    }

    private static string GenerateAddCheckConstraint(AddCheckConstraintOperation op) =>
        $"ALTER TABLE \"{op.Schema}\".\"{op.TableName}\" ADD CONSTRAINT \"{op.CheckConstraint.Name}\" CHECK ({op.CheckConstraint.Expression})";

    private static string GenerateAddUniqueConstraint(AddUniqueConstraintOperation op)
    {
        var uc = op.UniqueConstraint;
        var ucName = (uc.Name ?? $"UQ_{op.TableName}_{string.Join("_", uc.Columns)}");
        var ucCols = string.Join(", ", uc.Columns.Select(c => $"\"{c}\""));
        return $"ALTER TABLE \"{op.Schema}\".\"{op.TableName}\" ADD CONSTRAINT \"{ucName}\" UNIQUE ({ucCols})";
    }

    /// <summary>
    /// Map portable type to PostgreSQL type.
    /// </summary>
    public static string PortableTypeToPostgres(PortableType type) =>
        type switch
        {
            // Integer types
            TinyIntType => "SMALLINT",
            SmallIntType => "SMALLINT",
            IntType => "INTEGER",
            BigIntType => "BIGINT",
            BooleanType => "BOOLEAN",

            // Decimal types
            DecimalType(var p, var s) => $"NUMERIC({p},{s})",
            MoneyType => "NUMERIC(19,4)",
            SmallMoneyType => "NUMERIC(10,4)",
            FloatType => "REAL",
            DoubleType => "DOUBLE PRECISION",

            // String types
            CharType(var len) => $"CHAR({len})",
            VarCharType(var max) => $"VARCHAR({max})",
            NCharType(var len) => $"CHAR({len})",
            NVarCharType(var max) when max == int.MaxValue => "TEXT",
            NVarCharType(var max) => $"VARCHAR({max})",
            TextType => "TEXT",
            JsonType => "JSONB",
            XmlType => "XML",
            EnumType(var name, _) => name,

            // Binary types
            BinaryType(_) => "BYTEA",
            VarBinaryType(_) => "BYTEA",
            BlobType => "BYTEA",
            RowVersionType => "BYTEA",

            // Date/time types
            DateType => "DATE",
            TimeType(var p) => $"TIME({p})",
            DateTimeType(_) => "TIMESTAMP",
            DateTimeOffsetType => "TIMESTAMPTZ",

            // Other types
            UuidType => "UUID",
            GeometryType(var srid) => srid.HasValue ? $"GEOMETRY(Geometry,{srid})" : "GEOMETRY",
            GeographyType(var srid) => $"GEOGRAPHY(Geography,{srid})",
            VectorType(var dim) => string.Create(CultureInfo.InvariantCulture, $"vector({dim})"),

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

    /// <summary>
    /// Wraps any bare identifier in <paramref name="expression"/> that exactly
    /// matches a name in <paramref name="columnNames"/> with double-quotes,
    /// so PostgreSQL preserves case (unquoted identifiers are folded to
    /// lower case). Skips identifiers that are already quoted, inside
    /// single-quoted string literals, or that are prefixed by a `.` (which
    /// indicates they're already qualified, e.g. table.column).
    /// </summary>
    /// <remarks>
    /// Hand-rolled tokenizer (not regex) so we can correctly skip string
    /// literals and existing quoted identifiers.
    /// </remarks>
    internal static string QuoteIdentifiersInExpression(string expression, ISet<string> columnNames)
    {
        if (string.IsNullOrEmpty(expression) || columnNames.Count == 0)
        {
            return expression;
        }

        var sb = new StringBuilder(expression.Length + 16);
        var i = 0;
        while (i < expression.Length)
        {
            var c = expression[i];

            // Single-quoted string literal — copy verbatim until closing quote.
            if (c == '\'')
            {
                sb.Append(c);
                i++;
                while (i < expression.Length)
                {
                    sb.Append(expression[i]);
                    if (expression[i] == '\'')
                    {
                        // Postgres '' is an escaped single quote inside a literal.
                        if (i + 1 < expression.Length && expression[i + 1] == '\'')
                        {
                            sb.Append(expression[i + 1]);
                            i += 2;
                            continue;
                        }
                        i++;
                        break;
                    }
                    i++;
                }
                continue;
            }

            // Double-quoted identifier — copy verbatim, already quoted.
            if (c == '"')
            {
                sb.Append(c);
                i++;
                while (i < expression.Length)
                {
                    sb.Append(expression[i]);
                    if (expression[i] == '"')
                    {
                        i++;
                        break;
                    }
                    i++;
                }
                continue;
            }

            // Identifier candidate (letter or underscore start, then [a-zA-Z0-9_]).
            if (char.IsLetter(c) || c == '_')
            {
                var start = i;
                i++;
                while (
                    i < expression.Length
                    && (char.IsLetterOrDigit(expression[i]) || expression[i] == '_')
                )
                {
                    i++;
                }
                var word = expression[start..i];

                // Don't quote if the previous non-whitespace char is `.` —
                // it's already a qualified reference like `tbl.col`.
                var prevIdx = start - 1;
                while (prevIdx >= 0 && char.IsWhiteSpace(expression[prevIdx]))
                {
                    prevIdx--;
                }
                var qualified = prevIdx >= 0 && expression[prevIdx] == '.';

                if (!qualified && columnNames.Contains(word))
                {
                    sb.Append('"').Append(word).Append('"');
                }
                else
                {
                    sb.Append(word);
                }
                continue;
            }

            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }
}
