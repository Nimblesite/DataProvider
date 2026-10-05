using System.Text;
using Nimblesite.Lql.Core.FunctionMapping;
using Nimblesite.Sql.Model;

namespace Nimblesite.Lql.Postgres;

/// <summary>
/// Context for building PostgreSQL queries with proper table aliases and structure
/// </summary>
public sealed class PostgreSqlContext : ISqlContext
{
#pragma warning disable IDE0052 // Remove unread private members
    private readonly IFunctionMappingProvider _functionMappingProvider;
#pragma warning restore IDE0052 // Remove unread private members
    private readonly SelectStatementBuilder _builder = new();
    private readonly HashSet<string> _usedAliases = new(StringComparer.Ordinal);
    private string? _baseTable;
    private string? _baseAlias;

    /// <summary>
    /// Initializes a new instance of the PostgreSqlContext class
    /// </summary>
    /// <param name="functionMappingProvider">The function mapping provider (defaults to PostgreSQL provider)</param>
    public PostgreSqlContext(IFunctionMappingProvider? functionMappingProvider = null)
    {
        _functionMappingProvider =
            functionMappingProvider ?? PostgreSqlFunctionMappingLocal.Instance;
    }

    /// <summary>
    /// Gets a value indicating whether this query has joins
    /// </summary>
    public bool HasJoins => _builder.Build().HasJoins;

    /// <summary>
    /// Sets the base table for the query
    /// </summary>
    /// <param name="tableName">The base table name</param>
    public void SetBaseTable(string tableName)
    {
        _baseTable = tableName;
        _baseAlias = GenerateTableAlias(tableName);
        _builder.AddTable(tableName, _baseAlias);
    }

    /// <summary>
    /// Adds a JOIN to the query
    /// </summary>
    /// <param name="joinType">The type of join (INNER JOIN, LEFT JOIN, etc.)</param>
    /// <param name="tableName">The table to join</param>
    /// <param name="condition">The join condition</param>
    public void AddJoin(string joinType, string tableName, string? condition)
    {
        string alias;

        // Check if this is a subquery (starts with SELECT or parentheses)
        if (
            tableName.TrimStart().StartsWith("(SELECT", StringComparison.OrdinalIgnoreCase)
            || tableName.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
        )
        {
            // For subqueries, try to extract the base table name to generate alias
            alias = ExtractSubqueryAlias(tableName);
        }
        else
        {
            // For regular tables, use the standard alias generation
            alias = GenerateTableAlias(tableName);
        }

        _builder.AddTable(tableName, alias);
        if (!string.IsNullOrEmpty(condition))
        {
            _builder.AddJoin(_baseTable ?? "", tableName, condition, joinType);
        }
    }

    /// <summary>
    /// Extracts an appropriate alias for a subquery by finding the base table name
    /// </summary>
    /// <param name="subquerySql">The subquery SQL</param>
    /// <returns>The generated alias</returns>
    private string ExtractSubqueryAlias(string subquerySql)
    {
        // Try to find the FROM clause and extract the table name
        var upperSql = subquerySql.ToUpperInvariant();
        var fromIndex = upperSql.IndexOf("FROM", StringComparison.Ordinal);

        if (fromIndex >= 0)
        {
            // Find the table name after FROM
            var afterFrom = subquerySql[(fromIndex + 4)..].Trim();
            var words = afterFrom.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (words.Length > 0)
            {
                var tableName = words[0];
                // Generate alias from the actual table name
                return GenerateTableAlias(tableName);
            }
        }

        // Fallback to generic alias if we can't extract the table name
        return "sq"; // subquery
    }

    /// <summary>
    /// Sets the SELECT columns for the query
    /// </summary>
    /// <param name="columns">The columns to select</param>
    /// <param name="distinct">Whether to use DISTINCT</param>
    public void SetSelectColumns(IEnumerable<ColumnInfo> columns, bool distinct = false) =>
        _builder.WithSelectColumns(columns, distinct);

    /// <summary>
    /// Adds a WHERE condition
    /// </summary>
    /// <param name="condition">The condition to add</param>
    public void AddWhereCondition(WhereCondition condition) =>
        _builder.AddWhereCondition(condition);

    /// <summary>
    /// Adds GROUP BY columns
    /// </summary>
    /// <param name="columns">The columns to group by</param>
    public void AddGroupBy(IEnumerable<ColumnInfo> columns) => _builder.AddGroupBy(columns);

    /// <summary>
    /// Adds ORDER BY items
    /// </summary>
    /// <param name="orderItems">The order items (column, direction)</param>
    public void AddOrderBy(IEnumerable<(string Column, string Direction)> orderItems) =>
        StatementBuilderOperations.AddOrderBy(builder: _builder, orderItems: orderItems);

    /// <summary>
    /// Adds a HAVING condition
    /// </summary>
    /// <param name="condition">The having condition</param>
    public void AddHaving(string condition) => _builder.WithHaving(condition);

    /// <summary>
    /// Sets the LIMIT
    /// </summary>
    /// <param name="count">The limit count</param>
    public void SetLimit(string count) => _builder.WithLimit(count);

    /// <summary>
    /// Sets the OFFSET
    /// </summary>
    /// <param name="count">The offset count</param>
    public void SetOffset(string count) => _builder.WithOffset(count);

    /// <summary>
    /// Adds a UNION or UNION ALL
    /// </summary>
    /// <param name="query">The query to union with</param>
    /// <param name="isUnionAll">Whether this is UNION ALL</param>
    public void AddUnion(string query, bool isUnionAll) => _builder.AddUnion(query, isUnionAll);

    /// <summary>
    /// Generates the final SQL query
    /// </summary>
    /// <returns>The SQL query string</returns>
    public string GenerateSQL()
    {
        var statement = _builder.Build();

        return GenerateStatementSQL(statement);
    }

    /// <summary>Generates PostgreSQL SQL from an already-built statement.</summary>
    /// <param name="statement">The SQL statement to generate from</param>
    /// <returns>The PostgreSQL SQL string</returns>
    public static string ToPostgreSqlSql(SelectStatement statement) =>
        GenerateStatementSQL(statement);

    private static string GenerateStatementSQL(SelectStatement statement)
    {
        if (statement.Unions.Count > 0)
        {
            return GenerateUnionSQL(statement);
        }

        return GenerateSelectSQL(statement);
    }

    /// <summary>
    /// Generates a SELECT SQL query
    /// </summary>
    /// <param name="statement">The SQL statement to generate from</param>
    /// <returns>The SELECT SQL string</returns>
    private static string GenerateSelectSQL(SelectStatement statement)
    {
        var sql = new StringBuilder();

        // SELECT clause
        sql.Append(GenerateSelectClause(statement));

        // FROM clause
        sql.Append(GenerateFromClause(statement));

        // WHERE clause
        if (statement.WhereConditions.Count > 0)
        {
            sql.Append(GenerateWhereClause(statement));
        }

        // GROUP BY clause
        if (statement.GroupByColumns.Count > 0)
        {
            sql.Append(GenerateGroupByClause(statement));
        }

        // HAVING clause
        if (!string.IsNullOrEmpty(statement.HavingCondition))
        {
            sql.Append(GenerateHavingClause(statement));
        }

        // ORDER BY clause
        if (statement.OrderByItems.Count > 0)
        {
            sql.Append(GenerateOrderByClause(statement));
        }

        // LIMIT clause
        if (!string.IsNullOrEmpty(statement.Limit))
        {
            sql.Append(GenerateLimitClause(statement));
        }

        // OFFSET clause
        if (!string.IsNullOrEmpty(statement.Offset))
        {
            sql.Append(
                System.Globalization.CultureInfo.InvariantCulture,
                $"\nOFFSET {statement.Offset}"
            );
        }

        // Final pass: walk the entire generated SQL and quote any inline
        // `alias.Ident` substring whose tail contains uppercase. This
        // catches WHERE / JOIN ON / GROUP BY / HAVING / ORDER BY paths
        // where the raw expression text was emitted verbatim. The walker
        // skips characters inside string literals and already-quoted
        // identifiers so it's safe to apply once at the end.
        var result = QuoteInlineQualifiedIdents(sql.ToString());

        // Bug #22: when joins are present, the FROM/JOIN clauses emit
        // table aliases (`FROM users u INNER JOIN orders o ON ...`) but
        // the column refs in SELECT/WHERE/etc use the full table name
        // (`users.name`). Postgres rejects this — once you alias a
        // table, the original name is no longer a valid qualifier.
        // Build a map of table -> alias from the FROM/JOIN portion of
        // the generated SQL, then rewrite the rest of the body to use
        // aliases consistently.
        if (statement.HasJoins)
        {
            result = RewriteFullTableRefsToAliases(result, statement);
        }

        return result;
    }

    /// <summary>
    /// Walks the generated SQL and replaces any `tableName."Col"` or
    /// `tableName.col` reference with the corresponding aliased form
    /// using the alias declared in the FROM/JOIN clauses. Skips
    /// string literals and the FROM/JOIN-clause text itself (the
    /// declaration `FROM tableName alias` and `JOIN tableName alias`
    /// must keep the table name).
    /// </summary>
    private static string RewriteFullTableRefsToAliases(string sql, SelectStatement statement)
    {
        // Build name -> alias map from the statement's table list.
        var aliasMap = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var t in statement.Tables)
        {
            if (!string.IsNullOrEmpty(t.Alias) && !string.IsNullOrEmpty(t.Name))
            {
                aliasMap[t.Name] = t.Alias;
            }
        }
        if (aliasMap.Count == 0)
        {
            return sql;
        }

        var sb = new StringBuilder(sql.Length);
        var i = 0;
        while (i < sql.Length)
        {
            var c = sql[i];

            // Skip string literals.
            if (c == '\'')
            {
                sb.Append(c);
                i++;
                while (i < sql.Length)
                {
                    sb.Append(sql[i]);
                    if (sql[i] == '\'')
                    {
                        if (i + 1 < sql.Length && sql[i + 1] == '\'')
                        {
                            sb.Append(sql[i + 1]);
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

            // Quoted identifier `"foo"`. If followed by `.` it's a
            // `"MixedTable".` qualifier and can be rewritten to its alias.
            // Otherwise (followed by whitespace/end) it's the FROM/JOIN
            // declaration form `"MixedTable" f` and must be left alone.
            if (c == '"')
            {
                var quoteStart = i;
                i++;
                while (i < sql.Length && sql[i] != '"')
                {
                    i++;
                }
                if (i < sql.Length)
                {
                    i++; // consume closing quote
                }
                var quotedIdent = sql[quoteStart..i];
                var inner = quotedIdent.Length >= 2 ? quotedIdent[1..^1] : string.Empty;
                if (aliasMap.TryGetValue(inner, out var qAlias) && i < sql.Length && sql[i] == '.')
                {
                    sb.Append(qAlias);
                    continue;
                }
                sb.Append(quotedIdent);
                continue;
            }

            // Identifier candidate.
            if (char.IsLetter(c) || c == '_')
            {
                var start = i;
                i++;
                while (i < sql.Length && (char.IsLetterOrDigit(sql[i]) || sql[i] == '_'))
                {
                    i++;
                }
                var ident = sql[start..i];

                // Skip if this identifier is the FROM/JOIN declaration of
                // the same table. We detect this by looking back for
                // FROM/JOIN keyword and checking that what follows is
                // <table> <alias>. Easier heuristic: if the next non-
                // whitespace token after this ident is the alias literal
                // (a single word matching the alias map value), AND the
                // previous keyword was FROM/JOIN, treat as declaration.
                if (aliasMap.TryGetValue(ident, out var alias) && i < sql.Length && sql[i] == '.')
                {
                    // It's a `tableName.` reference — rewrite to alias.
                    sb.Append(alias);
                    continue;
                }

                sb.Append(ident);
                continue;
            }

            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }

    /// <summary>
    /// Generates a UNION SQL query
    /// </summary>
    /// <param name="statement">The SQL statement to generate from</param>
    /// <returns>The UNION SQL string</returns>
    private static string GenerateUnionSQL(SelectStatement statement)
    {
        var sql = new StringBuilder();

        // Add the main query
        sql.Append(GenerateSelectSQL(statement));

        // Add each union
        foreach (var union in statement.Unions)
        {
            sql.Append(union.IsUnionAll ? "\nUNION ALL\n" : "\nUNION\n");
            sql.Append(union.Query);
        }

        return sql.ToString();
    }

    /// <summary>
    /// Generates the SELECT clause
    /// </summary>
    /// <param name="statement">The SQL statement to generate from</param>
    /// <returns>The SELECT clause string</returns>
    private static string GenerateSelectClause(SelectStatement statement)
    {
        var selectKeyword = statement.IsDistinct ? "SELECT DISTINCT" : "SELECT";

        if (statement.SelectList.Count == 0)
        {
            return $"{selectKeyword} *";
        }

        var processedColumns = statement.SelectList.Select(GenerateColumnSqlWithAlias);

        // Use single-line format for simple queries (3 or fewer columns, no joins)
        // Use multi-line format for complex queries
        bool useMultiLine = statement.SelectList.Count > 3 || statement.HasJoins;

        if (useMultiLine)
        {
            // Format columns with proper indentation
            var formattedColumns = processedColumns.Select(col => $"    {col}");
            var columns = string.Join(",\n", formattedColumns);
            return $"{selectKeyword}\n{columns}";
        }
        else
        {
            // Single-line format
            var columns = string.Join(", ", processedColumns);
            return $"{selectKeyword} {columns}";
        }
    }

    /// <summary>
    /// Generates the FROM clause
    /// </summary>
    /// <param name="statement">The SQL statement to generate from</param>
    /// <returns>The FROM clause string</returns>
    private static string GenerateFromClause(SelectStatement statement)
    {
        var sql = new StringBuilder();

        var baseTable = statement.Tables.Count > 0 ? statement.Tables.First() : null;
        if (baseTable == null)
        {
            return "";
        }

        var quotedBase = FormatTableName(baseTable.Name);

        if (statement.HasJoins)
        {
            sql.Append(
                System.Globalization.CultureInfo.InvariantCulture,
                $"\nFROM {quotedBase} {baseTable.Alias}"
            );
        }
        else
        {
            sql.Append(System.Globalization.CultureInfo.InvariantCulture, $"\nFROM {quotedBase}");
        }

        // Add joins - get from Tables (skip first one which is base table)
        var joinTables = statement.Tables.Count > 1 ? statement.Tables.Skip(1) : [];
        var joinRelationships = statement.JoinGraph.GetRelationships();

        foreach (var table in joinTables)
        {
            var relationship = joinRelationships.FirstOrDefault(j => j.RightTable == table.Name);
            var joinType = relationship?.JoinType ?? "INNER JOIN";
            var quotedJoinTable = FormatTableName(table.Name);

            sql.Append(
                System.Globalization.CultureInfo.InvariantCulture,
                $"\n{joinType} {quotedJoinTable} {table.Alias}"
            );

            if (relationship != null && !string.IsNullOrEmpty(relationship.Condition))
            {
                var processedCondition = relationship.Condition;
                sql.Append(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $" ON {processedCondition}"
                );
            }
        }

        return sql.ToString();
    }

    /// <summary>
    /// Generates the WHERE clause
    /// </summary>
    /// <param name="statement">The SQL statement to generate from</param>
    /// <returns>The WHERE clause string</returns>
    private static string GenerateWhereClause(SelectStatement statement) =>
        $"\nWHERE {WhereConditionSql.Join(statement.WhereConditions, GenerateWhereConditionSql)}";

    /// <summary>
    /// Generates SQL for a single WHERE condition
    /// </summary>
    /// <param name="condition">The WHERE condition</param>
    /// <returns>The SQL string for the condition</returns>
    private static string GenerateWhereConditionSql(WhereCondition condition) =>
        condition switch
        {
            ComparisonCondition c => $"{GenerateColumnSql(c.Left)} {c.Operator.ToSql()} {c.Right}",
            LogicalOperator lo => lo.ToSql(),
            Parenthesis p => p.IsOpening ? "(" : ")",
            ExpressionCondition e => e.Expression,
            _ => "/*UNKNOWN_WHERE*/",
        };

    /// <summary>
    /// Generates SQL for a ColumnInfo. Bare column names get double-quoted
    /// so PostgreSQL preserves their case (matches the FormatTableName
    /// behaviour above). ExpressionColumn values pass through
    /// QuoteIdentifier as well so qualified references like
    /// `reference_table.Id` get the trailing component quoted.
    /// </summary>
    /// <param name="columnInfo">The column info</param>
    /// <returns>The SQL string for the column</returns>
    private static string GenerateColumnSql(ColumnInfo columnInfo) =>
        columnInfo switch
        {
            NamedColumn n => string.IsNullOrEmpty(n.TableAlias)
                ? QuoteIdentifier(n.Name)
                // Bug #24: also quote the TableAlias if it needs it.
                // When the FROM clause uses a quoted mixed-case table name
                // (e.g. `FROM "MixedTable"`), the column qualifier must
                // also be quoted to look up the same table.
                : $"{QuoteBareIdentifier(n.TableAlias)}.{QuoteIdentifier(n.Name)}",
            WildcardColumn w => string.IsNullOrEmpty(w.TableAlias)
                ? "*"
                : $"{QuoteBareIdentifier(w.TableAlias)}.*",
            ExpressionColumn e => QuoteIdentifier(e.Expression),
            SubQueryColumn s => $"({s.SubQuery})",
            _ => "/*UNKNOWN_COLUMN*/",
        };

    /// <summary>
    /// Quotes a bare identifier (no dots) when its case requires it.
    /// Identifiers that are already quoted are passed through.
    /// </summary>
    private static string QuoteBareIdentifier(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            return identifier;
        }
        if (identifier.StartsWith('"'))
        {
            return identifier;
        }
        return NeedsQuoting(identifier) ? $"\"{identifier}\"" : identifier;
    }

    /// <summary>
    /// Generates SQL for a ColumnInfo with alias if present
    /// </summary>
    /// <param name="columnInfo">The column info</param>
    /// <returns>The SQL string for the column with alias</returns>
    private static string GenerateColumnSqlWithAlias(ColumnInfo columnInfo)
    {
        var sql = GenerateColumnSql(columnInfo);
        return string.IsNullOrEmpty(columnInfo.Alias) ? sql : $"{sql} AS {columnInfo.Alias}";
    }

    /// <summary>
    /// Generates a table alias from a table name. Bug #20: tracks used
    /// aliases per-context and appends a digit suffix on collision so
    /// two tables starting with the same letter (e.g. account + address)
    /// don't end up with the same alias.
    /// </summary>
    /// <param name="tableName">The table name</param>
    /// <returns>The generated alias</returns>
    private string GenerateTableAlias(string tableName)
    {
        ArgumentNullException.ThrowIfNull(tableName);
        var baseAlias = tableName.Length > 0 ? tableName[0].ToString().ToLowerInvariant() : "t";

        if (_usedAliases.Add(baseAlias))
        {
            return baseAlias;
        }

        // Collision: append a numeric suffix until we find a free slot.
        var suffix = 2;
        while (true)
        {
            var candidate =
                baseAlias + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (_usedAliases.Add(candidate))
            {
                return candidate;
            }
            suffix++;
        }
    }

    /// <summary>
    /// Formats a table name for PostgreSQL. PostgreSQL folds unquoted
    /// identifiers to lower case, so any identifier that contains an
    /// uppercase character (e.g. `MixedTable`) MUST be double-quoted
    /// to survive a round-trip. Identifiers that are already lowercase
    /// (the previous behaviour) are emitted unquoted to preserve the
    /// existing test fixture output.
    /// </summary>
    private static string FormatTableName(string tableName) =>
        NeedsQuoting(tableName) ? $"\"{tableName}\"" : tableName;

    /// <summary>
    /// Quotes a bare identifier (column name) when it contains characters
    /// that PostgreSQL would fold (uppercase letters). For complex
    /// expressions (anything with whitespace, parentheses, operators
    /// or keywords) we walk the string and quote each `prefix.Tail`
    /// substring where the tail is a bare ident containing uppercase.
    /// </summary>
    private static string QuoteIdentifier(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            return identifier;
        }
        if (identifier.StartsWith('"'))
        {
            return identifier;
        }

        // Simple bare identifier (no `.`, no whitespace, no operators).
        if (IsSimpleBareIdent(identifier))
        {
            return NeedsQuoting(identifier) ? $"\"{identifier}\"" : identifier;
        }

        // Simple qualified reference `prefix.tail` where both halves
        // are bare idents.
        if (IsSimpleQualifiedIdent(identifier, out var prefix, out var tail))
        {
            // Quote either side that needs it. Bug #24: PG folds unquoted
            // table-name qualifiers, so a quoted FROM table requires the
            // qualifier on the column ref to also be quoted.
            var prefixNeeds = NeedsQuoting(prefix);
            var tailNeeds = tail != "*" && NeedsQuoting(tail);
            if (!prefixNeeds && !tailNeeds)
            {
                return identifier;
            }
            var quotedPrefix = prefixNeeds ? $"\"{prefix}\"" : prefix;
            var quotedTail = tail == "*" ? "*" : (tailNeeds ? $"\"{tail}\"" : tail);
            return $"{quotedPrefix}.{quotedTail}";
        }

        // Complex expression: walk and quote inline `alias.Ident`
        // substrings where Ident contains uppercase.
        return QuoteInlineQualifiedIdents(identifier);
    }

    /// <summary>
    /// True when <paramref name="s"/> is a single bare identifier:
    /// only ASCII letters, digits, and underscore, starting with a letter
    /// or underscore.
    /// </summary>
    private static bool IsSimpleBareIdent(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return false;
        }
        var first = s[0];
        if (!(char.IsLetter(first) || first == '_'))
        {
            return false;
        }
        for (var i = 1; i < s.Length; i++)
        {
            var c = s[i];
            if (!(char.IsLetterOrDigit(c) || c == '_'))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// True when <paramref name="s"/> is exactly `prefix.tail` with both
    /// halves being bare identifiers (or `*` as the tail).
    /// </summary>
    private static bool IsSimpleQualifiedIdent(string s, out string prefix, out string tail)
    {
        prefix = string.Empty;
        tail = string.Empty;
        var dot = s.IndexOf('.', StringComparison.Ordinal);
        if (dot <= 0 || dot == s.Length - 1)
        {
            return false;
        }
        var first = s[..dot];
        var second = s[(dot + 1)..];
        if (second.Contains('.', StringComparison.Ordinal))
        {
            return false;
        }
        if (!IsSimpleBareIdent(first))
        {
            return false;
        }
        if (second != "*" && !IsSimpleBareIdent(second))
        {
            return false;
        }
        prefix = first;
        tail = second;
        return true;
    }

    /// <summary>
    /// Walks an arbitrary expression string and rewrites any inline
    /// substring matching `alias.Ident` (where Ident contains an
    /// uppercase letter) into `alias."Ident"`. Skips characters inside
    /// single-quoted string literals or already-quoted identifiers.
    /// </summary>
    private static string QuoteInlineQualifiedIdents(string expression)
    {
        var sb = new StringBuilder(expression.Length + 8);
        var i = 0;
        while (i < expression.Length)
        {
            var c = expression[i];

            if (c == '\'')
            {
                sb.Append(c);
                i++;
                while (i < expression.Length)
                {
                    sb.Append(expression[i]);
                    if (expression[i] == '\'')
                    {
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
                var firstIdent = expression[start..i];

                if (i < expression.Length && expression[i] == '.')
                {
                    var tailStart = i + 1;
                    if (
                        tailStart < expression.Length
                        && (char.IsLetter(expression[tailStart]) || expression[tailStart] == '_')
                    )
                    {
                        var tailEnd = tailStart + 1;
                        while (
                            tailEnd < expression.Length
                            && (
                                char.IsLetterOrDigit(expression[tailEnd])
                                || expression[tailEnd] == '_'
                            )
                        )
                        {
                            tailEnd++;
                        }
                        var tailIdent = expression[tailStart..tailEnd];

                        // Bug #24: also quote the prefix when it needs
                        // quoting (e.g. `MixedTable.Id` ->
                        // `"MixedTable"."Id"`). PG folds the unquoted
                        // table-name qualifier to lower case otherwise
                        // and the lookup against a quoted FROM table
                        // ("MixedTable") fails.
                        if (NeedsQuoting(firstIdent))
                        {
                            sb.Append('"').Append(firstIdent).Append('"');
                        }
                        else
                        {
                            sb.Append(firstIdent);
                        }
                        sb.Append('.');
                        if (NeedsQuoting(tailIdent))
                        {
                            sb.Append('"').Append(tailIdent).Append('"');
                        }
                        else
                        {
                            sb.Append(tailIdent);
                        }
                        i = tailEnd;
                        continue;
                    }
                }

                sb.Append(firstIdent);
                continue;
            }

            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }

    /// <summary>
    /// Returns true when an identifier contains an uppercase ASCII letter,
    /// meaning Postgres would fold it to lower case if left unquoted.
    /// </summary>
    private static bool NeedsQuoting(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            return false;
        }
        for (var i = 0; i < identifier.Length; i++)
        {
            var c = identifier[i];
            if (c >= 'A' && c <= 'Z')
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Generates the GROUP BY clause
    /// </summary>
    /// <param name="statement">The SQL statement to generate from</param>
    /// <returns>The GROUP BY clause string</returns>
    private static string GenerateGroupByClause(SelectStatement statement)
    {
        var processedColumns = statement.GroupByColumns.Select(GenerateColumnSql);

        return $"\nGROUP BY {string.Join(", ", processedColumns)}";
    }

    /// <summary>
    /// Generates the HAVING clause
    /// </summary>
    /// <param name="statement">The SQL statement to generate from</param>
    /// <returns>The HAVING clause string</returns>
    private static string GenerateHavingClause(SelectStatement statement)
    {
        if (string.IsNullOrEmpty(statement.HavingCondition))
        {
            return "";
        }

        var processedCondition = statement.HavingCondition;

        return $"\nHAVING {processedCondition}";
    }

    /// <summary>
    /// Generates the ORDER BY clause
    /// </summary>
    /// <param name="statement">The SQL statement to generate from</param>
    /// <returns>The ORDER BY clause string</returns>
    private static string GenerateOrderByClause(SelectStatement statement)
    {
        var processedItems = statement.OrderByItems.Select(item =>
        {
            var processedColumn = GenerateColumnSql(ColumnInfo.Named(item.Column));
            return $"{processedColumn} {item.Direction}";
        });

        return $"\nORDER BY {string.Join(", ", processedItems)}";
    }

    /// <summary>
    /// Generates the LIMIT clause
    /// </summary>
    /// <param name="statement">The SQL statement to generate from</param>
    /// <returns>The LIMIT clause string</returns>
    private static string GenerateLimitClause(SelectStatement statement) =>
        $"\nLIMIT {statement.Limit}";
}
