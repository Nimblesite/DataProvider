using Nimblesite.Sql.Model;

namespace Nimblesite.Lql.SQLite;

/// <summary>
/// Context for building SQLite queries with proper table aliases and structure
/// </summary>
public sealed class SQLiteContext : ISqlContext
{
    private readonly SelectStatementBuilder _builder = new();
    private string? _baseTable;

    /// <summary>
    /// Initializes a new instance of the SQLiteContext class
    /// </summary>
    public SQLiteContext() { }

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
        _builder.AddTable(tableName);
    }

    /// <summary>
    /// Adds a JOIN to the query
    /// </summary>
    /// <param name="joinType">The type of join (INNER JOIN, LEFT JOIN, etc.)</param>
    /// <param name="tableName">The table to join</param>
    /// <param name="condition">The join condition</param>
    public void AddJoin(string joinType, string tableName, string? condition)
    {
        _builder.AddTable(tableName);
        if (!string.IsNullOrEmpty(condition) && !string.IsNullOrEmpty(_baseTable))
        {
            _builder.AddJoin(_baseTable, tableName, condition, joinType);
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
        // Generate SQL from the builder instead of creating another statement
        var selectStatement = _builder.Build();
        return GenerateSQLiteSQL(selectStatement);
    }

    /// <summary>
    /// Processes a pipeline and returns the generated SQL
    /// </summary>
    /// <param name="pipeline">The pipeline to process</param>
    /// <returns>The generated SQLite SQL</returns>
    public string ProcessPipeline(Pipeline pipeline)
    {
        // Process each step in the pipeline
        foreach (var step in pipeline.Steps)
        {
            ProcessStep(step);
        }

        // Generate SQL from the builder instead of recursing
        var selectStatement = _builder.Build();
        return GenerateSQLiteSQL(selectStatement);
    }

    private void ProcessStep(IStep step)
    {
        switch (step)
        {
            case SelectStep selectStep:
                ProcessSelectStep(selectStep);
                break;
            case FilterStep filterStep:
                ProcessFilterStep(filterStep);
                break;
            case JoinStep joinStep:
                ProcessJoinStep(joinStep);
                break;
            case OrderByStep orderByStep:
                ProcessOrderByStep(orderByStep);
                break;
            case LimitStep limitStep:
                ProcessLimitStep(limitStep);
                break;
            default:
                // Handle other step types as needed
                break;
        }
    }

    private void ProcessSelectStep(SelectStep selectStep)
    {
        // Add columns to select
        foreach (var column in selectStep.Columns)
        {
            _builder.AddSelectColumn(column);
        }
    }

    private void ProcessFilterStep(FilterStep filterStep) =>
        // Add WHERE conditions - FilterStep.Condition is already a WhereCondition
        _builder.AddWhereCondition(filterStep.Condition);

    private void ProcessJoinStep(JoinStep joinStep)
    {
        var join = joinStep.JoinRelationship;
        _builder.AddTable(join.RightTable);
        if (!string.IsNullOrEmpty(join.Condition) && !string.IsNullOrEmpty(_baseTable))
        {
            _builder.AddJoin(_baseTable, join.RightTable, join.Condition, join.JoinType);
        }
    }

    private void ProcessOrderByStep(OrderByStep orderByStep)
    {
        // Add ORDER BY
        foreach (var (column, direction) in orderByStep.OrderItems)
        {
            _builder.AddOrderBy(column, direction);
        }
    }

    private void ProcessLimitStep(LimitStep limitStep) =>
        // SQLite supports LIMIT
        _builder.WithLimit(limitStep.Count.ToString());

    /// <summary>
    /// Public helper to generate SQLite SQL from an existing SelectStatement
    /// </summary>
    /// <param name="statement">The statement to generate SQL for</param>
    /// <returns>SQLite SQL string</returns>
    public static string ToSQLiteSql(SelectStatement statement) => GenerateSQLiteSQL(statement);

    /// <summary>
    /// Generates SQLite SQL from a SelectStatement
    /// </summary>
    /// <param name="statement">The statement to generate SQL for</param>
    /// <returns>SQLite SQL string</returns>
    private static string GenerateSQLiteSQL(SelectStatement statement)
    {
        var sql = new System.Text.StringBuilder();
        var separator = ClauseSeparator(statement);

        // SELECT clause
        sql.Append("SELECT ");
        if (statement.IsDistinct)
        {
            sql.Append("DISTINCT ");
        }

        if (statement.SelectList.Count > 0)
        {
            sql.Append(string.Join(", ", statement.SelectList.Select(FormatColumn)));
        }
        else
        {
            sql.Append('*');
        }

        // FROM clause with JOINs
        if (statement.Tables.Count > 0)
        {
            sql.Append(separator).Append("FROM ");
            var baseTable = statement.Tables.First();
            sql.Append(baseTable.Name);
            if (!string.IsNullOrEmpty(baseTable.Alias))
            {
                sql.Append(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $" {baseTable.Alias}"
                );
            }

            // Add JOINs - get from JoinGraph
            var joinTables = statement.Tables.Count > 1 ? statement.Tables.Skip(1) : [];
            var joinRelationships = statement.JoinGraph.GetRelationships();

            foreach (var table in joinTables)
            {
                var relationship = joinRelationships.FirstOrDefault(j =>
                    j.RightTable == table.Name
                );
                var joinType = relationship?.JoinType ?? "INNER";
                var fullJoinType = joinType.Contains("JOIN", StringComparison.OrdinalIgnoreCase)
                    ? joinType
                    : $"{joinType} JOIN";

                sql.Append(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"{separator}{fullJoinType} {table.Name}"
                );

                if (!string.IsNullOrEmpty(table.Alias))
                {
                    sql.Append(
                        System.Globalization.CultureInfo.InvariantCulture,
                        $" {table.Alias}"
                    );
                }

                if (relationship != null && !string.IsNullOrEmpty(relationship.Condition))
                {
                    sql.Append(
                        System.Globalization.CultureInfo.InvariantCulture,
                        $" ON {relationship.Condition}"
                    );
                }
            }
        }

        // WHERE clause
        if (statement.WhereConditions.Count > 0)
        {
            sql.Append(separator).Append("WHERE ");
            sql.Append(WhereConditionSql.Join(statement.WhereConditions, FormatWhereCondition));
        }

        // GROUP BY clause
        if (statement.GroupByColumns.Count > 0)
        {
            sql.Append(separator).Append("GROUP BY ");
            sql.Append(string.Join(", ", statement.GroupByColumns.Select(FormatColumn)));
        }

        // HAVING clause
        if (!string.IsNullOrEmpty(statement.HavingCondition))
        {
            sql.Append(separator).Append("HAVING ");
            sql.Append(statement.HavingCondition);
        }

        // ORDER BY clause
        if (statement.OrderByItems.Count > 0)
        {
            sql.Append(separator).Append("ORDER BY ");
            sql.Append(
                string.Join(", ", statement.OrderByItems.Select(o => $"{o.Column} {o.Direction}"))
            );
        }

        // LIMIT clause
        if (!string.IsNullOrEmpty(statement.Limit))
        {
            sql.Append(separator).Append("LIMIT ");
            sql.Append(statement.Limit);
        }

        // OFFSET clause
        if (!string.IsNullOrEmpty(statement.Offset))
        {
            sql.Append(separator).Append("OFFSET ");
            sql.Append(statement.Offset);
        }

        return sql.ToString();
    }

    /// <summary>
    /// Statements are single-line, except that a WHERE holding a nested (multi-line)
    /// subquery puts each clause on its own line, matching the other dialects'
    /// subquery layout. Implements [LQL-SUBQUERY-LAYOUT].
    /// </summary>
    private static string ClauseSeparator(SelectStatement statement) =>
        statement.WhereConditions.Any(c =>
            c is ExpressionCondition e && e.Expression.Contains('\n', StringComparison.Ordinal)
        )
            ? "\n"
            : " ";

    /// <summary>
    /// Formats a column for SQL output
    /// </summary>
    /// <param name="column">The column to format</param>
    /// <returns>Formatted column string</returns>
    private static string FormatColumn(ColumnInfo column) =>
        column switch
        {
            NamedColumn named => string.IsNullOrEmpty(named.Alias)
                ? (
                    string.IsNullOrEmpty(named.TableAlias)
                        ? named.Name
                        : $"{named.TableAlias}.{named.Name}"
                )
                : (
                    string.IsNullOrEmpty(named.TableAlias)
                        ? $"{named.Name} AS {named.Alias}"
                        : $"{named.TableAlias}.{named.Name} AS {named.Alias}"
                ),
            ExpressionColumn expr => string.IsNullOrEmpty(expr.Alias)
                ? expr.Expression
                : $"{expr.Expression} AS {expr.Alias}",
            WildcardColumn wildcard => wildcard.TableAlias != null
                ? $"{wildcard.TableAlias}.*"
                : "*",
            _ => column.ToString() ?? "NULL",
        };

    /// <summary>
    /// Formats a WHERE condition for SQL output
    /// </summary>
    /// <param name="condition">The condition to format</param>
    /// <returns>Formatted condition string</returns>
    private static string FormatWhereCondition(WhereCondition condition) =>
        condition switch
        {
            ComparisonCondition c => $"{FormatColumn(c.Left)} {c.Operator.ToSql()} {c.Right}",
            LogicalOperator lo => lo.ToSql(),
            Parenthesis p => p.IsOpening ? "(" : ")",
            ExpressionCondition e => e.Expression,
            _ => "/*UNKNOWN_WHERE*/",
        };
}
