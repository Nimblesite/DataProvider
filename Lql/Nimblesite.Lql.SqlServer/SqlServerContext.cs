using Nimblesite.Lql.Core.FunctionMapping;
using Nimblesite.Sql.Model;

namespace Nimblesite.Lql.SqlServer;

/// <summary>
/// SQL Server context implementation for generating SQL Server-specific SQL
/// </summary>
public sealed class SqlServerContext : ISqlContext
{
    private readonly IFunctionMappingProvider _functionMappingProvider;
    private readonly SelectStatementBuilder _builder = new();
    private string? _baseTable;
    private string? _baseAlias;

    /// <summary>
    /// Initializes a new instance of the SqlServerContext class
    /// </summary>
    /// <param name="functionMappingProvider">The function mapping provider (defaults to SQL Server provider)</param>
    public SqlServerContext(IFunctionMappingProvider? functionMappingProvider = null)
    {
        _functionMappingProvider = functionMappingProvider ?? SqlServerFunctionMapping.Instance;
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
        ArgumentNullException.ThrowIfNull(tableName);

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
        ArgumentNullException.ThrowIfNull(tableName);

        var alias = GenerateTableAlias(tableName);
        _builder.AddTable(tableName, alias);
        if (!string.IsNullOrEmpty(condition))
        {
            _builder.AddJoin(_baseTable ?? "", tableName, condition, joinType);
        }
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
    public void AddOrderBy(IEnumerable<(string Column, string Direction)> orderItems)
    {
        foreach (var (column, direction) in orderItems)
        {
            _builder.AddOrderBy(column, direction);
        }
    }

    /// <summary>
    /// Adds a HAVING condition
    /// </summary>
    /// <param name="condition">The having condition</param>
    public void AddHaving(string condition)
    {
        ArgumentNullException.ThrowIfNull(condition);

        _builder.WithHaving(condition);
    }

    /// <summary>
    /// Sets the LIMIT (TOP for SQL Server)
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
    /// <returns>The SQL Server query string</returns>
    public string GenerateSQL()
    {
        var statement = _builder.Build();

        return GenerateStatementSQL(statement);
    }

    /// <summary>
    /// Generates SQL for an already-built statement, appending UNIONs
    /// ([DP-SQL-MODEL-DIALECTS])
    /// </summary>
    /// <param name="statement">The SQL statement to generate from</param>
    /// <returns>The SQL string</returns>
    private string GenerateStatementSQL(SelectStatement statement)
    {
        var sql = GenerateSelectSQL(statement);

        // Add UNIONs (only for non-INSERT queries)
        foreach (var union in statement.Unions)
        {
            var unionType = union.IsUnionAll ? "UNION ALL" : "UNION";
            sql += $"\n\n{unionType}\n\n{union.Query}";
        }

        return sql;
    }

    /// <summary>
    /// Generates SQL Server SQL directly from a SelectStatement.
    /// Implements [DP-SQL-MODEL-DIALECTS].
    /// </summary>
    /// <param name="statement">The SQL statement to generate from</param>
    /// <returns>The SQL Server SQL string</returns>
    public static string ToSqlServerSql(SelectStatement statement) =>
        new SqlServerContext().GenerateStatementSQL(statement);

    /// <summary>
    /// Generates the SELECT portion of the SQL
    /// </summary>
    /// <param name="statement">The SQL statement to generate from</param>
    /// <returns>The SELECT SQL</returns>
    private string GenerateSelectSQL(SelectStatement statement)
    {
        var selectClause = GenerateSelectClause(statement);
        var fromClause = GenerateFromClause(statement);
        var whereClause = GenerateWhereClause(statement);
        var groupByClause = GenerateGroupByClause(statement);
        var havingClause = GenerateHavingClause(statement);
        var orderByClause = GenerateOrderByClause(statement);
        var limitClause = GenerateLimitClause(statement);

        var parts = new List<string> { selectClause, fromClause };

        if (!string.IsNullOrEmpty(whereClause))
            parts.Add(whereClause);
        if (!string.IsNullOrEmpty(groupByClause))
            parts.Add(groupByClause);
        if (!string.IsNullOrEmpty(havingClause))
            parts.Add(havingClause);
        if (!string.IsNullOrEmpty(orderByClause))
            parts.Add(orderByClause);
        if (!string.IsNullOrEmpty(limitClause))
            parts.Add(limitClause);

        return string.Join("\n", parts);
    }

    /// <summary>
    /// Generates the SELECT clause with SQL Server-specific TOP handling
    /// </summary>
    /// <param name="statement">The SQL statement to generate from</param>
    /// <returns>The SELECT clause</returns>
    private static string GenerateSelectClause(SelectStatement statement)
    {
        var distinctKeyword = statement.IsDistinct ? "DISTINCT " : "";
        // Only use TOP if LIMIT is present but OFFSET is not (since OFFSET requires FETCH NEXT syntax)
        var topClause =
            !string.IsNullOrEmpty(statement.Limit) && string.IsNullOrEmpty(statement.Offset)
                ? $"TOP {statement.Limit} "
                : "";

        if (statement.SelectList.Count == 0)
        {
            return $"SELECT {topClause}{distinctKeyword}*";
        }

        // Process columns using local method
        var processedColumns = statement.SelectList.Select(GenerateColumnSqlWithAlias);

        // Use multi-line format for SELECT clauses with 7+ columns
        if (statement.SelectList.Count >= 7)
        {
            var indentedColumns = processedColumns.Select(col => $"    {col}");
            return $"SELECT {topClause}{distinctKeyword}\n{string.Join(",\n", indentedColumns)}";
        }

        return $"SELECT {topClause}{distinctKeyword}{string.Join(", ", processedColumns)}";
    }

    /// <summary>
    /// Generates the FROM clause
    /// </summary>
    /// <param name="statement">The SQL statement to generate from</param>
    /// <returns>The FROM clause</returns>
    private static string GenerateFromClause(SelectStatement statement)
    {
        var baseTable = statement.Tables.Count > 0 ? statement.Tables.First() : null;
        if (baseTable == null)
            return "-- No base table";

        var fromClause = $"FROM {baseTable.Name}";

        // Add table alias if there are joins (multi-table queries)
        if (statement.HasJoins && baseTable.Alias != null)
        {
            fromClause += $" {baseTable.Alias}";
        }

        // Add joins - get from Tables (skip first one which is base table)
        var joinTables = statement.Tables.Count > 1 ? statement.Tables.Skip(1) : [];
        var joinRelationships = statement.JoinGraph.GetRelationships();

        foreach (var table in joinTables)
        {
            var relationship = joinRelationships.FirstOrDefault(j => j.RightTable == table.Name);
            var joinType = relationship?.JoinType ?? "INNER JOIN";

            fromClause += $"\n{joinType} {table.Name}";
            if (table.Alias != null)
            {
                fromClause += $" {table.Alias}";
            }
            if (relationship != null && !string.IsNullOrEmpty(relationship.Condition))
            {
                var processedCondition = relationship.Condition;
                fromClause += $" ON {processedCondition}";
            }
        }

        return fromClause;
    }

    /// <summary>
    /// Generates the WHERE clause
    /// </summary>
    /// <param name="statement">The SQL statement to generate from</param>
    /// <returns>The WHERE clause or empty string</returns>
    private static string GenerateWhereClause(SelectStatement statement) =>
        statement.WhereConditions.Count == 0
            ? ""
            : $"WHERE {WhereConditionSql.Join(statement.WhereConditions, GenerateWhereConditionSql)}";

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
    /// Generates SQL for a ColumnInfo
    /// </summary>
    /// <param name="columnInfo">The column info</param>
    /// <returns>The SQL string for the column</returns>
    private static string GenerateColumnSql(ColumnInfo columnInfo) =>
        columnInfo switch
        {
            NamedColumn n => string.IsNullOrEmpty(n.TableAlias)
                ? n.Name
                : $"{n.TableAlias}.{n.Name}",
            WildcardColumn w => string.IsNullOrEmpty(w.TableAlias) ? "*" : $"{w.TableAlias}.*",
            ExpressionColumn e => e.Expression,
            SubQueryColumn s => $"({s.SubQuery})",
            _ => "/*UNKNOWN_COLUMN*/",
        };

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
    /// Generates a table alias from a table name
    /// </summary>
    /// <param name="tableName">The table name</param>
    /// <returns>The generated alias</returns>
    private static string GenerateTableAlias(string tableName)
    {
        ArgumentNullException.ThrowIfNull(tableName);
        // Use first letter of the table name (to match expected test output)
        return tableName.Length > 0 ? tableName[0].ToString().ToLowerInvariant() : "t";
    }

    /// <summary>
    /// Generates the GROUP BY clause
    /// </summary>
    /// <param name="statement">The SQL statement to generate from</param>
    /// <returns>The GROUP BY clause or empty string</returns>
    private static string GenerateGroupByClause(SelectStatement statement)
    {
        if (statement.GroupByColumns.Count == 0)
            return "";
        var processedColumns = statement.GroupByColumns.Select(GenerateColumnSql);
        return $"GROUP BY {string.Join(", ", processedColumns)}";
    }

    /// <summary>
    /// Generates the HAVING clause
    /// </summary>
    /// <param name="statement">The SQL statement to generate from</param>
    /// <returns>The HAVING clause or empty string</returns>
    private static string GenerateHavingClause(SelectStatement statement)
    {
        if (string.IsNullOrEmpty(statement.HavingCondition))
            return "";
        var processedCondition = statement.HavingCondition;
        return $"HAVING {processedCondition}";
    }

    /// <summary>
    /// Generates the ORDER BY clause
    /// </summary>
    /// <param name="statement">The SQL statement to generate from</param>
    /// <returns>The ORDER BY clause or empty string</returns>
    private static string GenerateOrderByClause(SelectStatement statement)
    {
        if (statement.OrderByItems.Count == 0)
            return "";

        var orderItems = statement.OrderByItems.Select(item =>
        {
            var column = GenerateColumnSql(ColumnInfo.Named(item.Column));
            var direction = string.IsNullOrEmpty(item.Direction) ? "" : $" {item.Direction}";
            return $"{column}{direction}";
        });

        return $"ORDER BY {string.Join(", ", orderItems)}";
    }

    /// <summary>
    /// Generates the LIMIT clause (handled via TOP in SELECT for SQL Server)
    /// </summary>
    /// <param name="statement">The SQL statement to generate from</param>
    /// <returns>The LIMIT clause or empty string</returns>
    private string GenerateLimitClause(SelectStatement statement)
    {
        if (!string.IsNullOrEmpty(statement.Offset))
        {
            var offsetClause = _functionMappingProvider.FormatOffsetClause(statement.Offset);
            // If both OFFSET and LIMIT are present, add FETCH NEXT clause
            if (!string.IsNullOrEmpty(statement.Limit))
            {
                return $"{offsetClause}\nFETCH NEXT {statement.Limit} ROWS ONLY";
            }
            return offsetClause;
        }
        return "";
    }
}
