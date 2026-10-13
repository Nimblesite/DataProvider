using Nimblesite.Sql.Model;

namespace Nimblesite.Lql.Core;

/// <summary>
/// Shared <see cref="ISqlContext"/> facade over a <see cref="SelectStatementBuilder"/>.
/// Every dialect accumulates the same statement-builder calls; only base-table/alias
/// handling and final SQL rendering differ. Implements [DP-SQL-MODEL-DIALECTS].
/// </summary>
public abstract class SelectContextBase : ISqlContext
{
    /// <summary>
    /// Gets the statement builder this dialect context accumulates into.
    /// </summary>
    protected SelectStatementBuilder Builder { get; } = new();

    /// <summary>
    /// Gets a value indicating whether this query has joins
    /// </summary>
    public bool HasJoins => Builder.Build().HasJoins;

    /// <summary>
    /// Sets the SELECT columns for the query
    /// </summary>
    /// <param name="columns">The columns to select</param>
    /// <param name="distinct">Whether to use DISTINCT</param>
    public void SetSelectColumns(IEnumerable<ColumnInfo> columns, bool distinct = false) =>
        Builder.WithSelectColumns(columns, distinct);

    /// <summary>
    /// Adds a WHERE condition
    /// </summary>
    /// <param name="condition">The condition to add</param>
    public void AddWhereCondition(WhereCondition condition) => Builder.AddWhereCondition(condition);

    /// <summary>
    /// Adds GROUP BY columns
    /// </summary>
    /// <param name="columns">The columns to group by</param>
    public void AddGroupBy(IEnumerable<ColumnInfo> columns) => Builder.AddGroupBy(columns);

    /// <summary>
    /// Adds ORDER BY items
    /// </summary>
    /// <param name="orderItems">The order items (column, direction)</param>
    public void AddOrderBy(IEnumerable<(string Column, string Direction)> orderItems) =>
        StatementBuilderOperations.AddOrderBy(builder: Builder, orderItems: orderItems);

    /// <summary>
    /// Adds a HAVING condition
    /// </summary>
    /// <param name="condition">The having condition</param>
    public virtual void AddHaving(string condition) => Builder.WithHaving(condition);

    /// <summary>
    /// Sets the LIMIT (or TOP for SQL Server)
    /// </summary>
    /// <param name="count">The limit count</param>
    public void SetLimit(string count) => Builder.WithLimit(count);

    /// <summary>
    /// Sets the OFFSET
    /// </summary>
    /// <param name="count">The offset count</param>
    public void SetOffset(string count) => Builder.WithOffset(count);

    /// <summary>
    /// Adds a UNION or UNION ALL
    /// </summary>
    /// <param name="query">The query to union with</param>
    /// <param name="isUnionAll">Whether this is UNION ALL</param>
    public void AddUnion(string query, bool isUnionAll) => Builder.AddUnion(query, isUnionAll);

    /// <summary>
    /// Sets the base table for the query
    /// </summary>
    /// <param name="tableName">The base table name</param>
    public abstract void SetBaseTable(string tableName);

    /// <summary>
    /// Adds a JOIN to the query
    /// </summary>
    /// <param name="joinType">The type of join (INNER JOIN, LEFT JOIN, etc.)</param>
    /// <param name="tableName">The table to join</param>
    /// <param name="condition">The join condition</param>
    public abstract void AddJoin(string joinType, string tableName, string? condition);

    /// <summary>
    /// Generates the final SQL query
    /// </summary>
    /// <returns>The SQL query string</returns>
    public abstract string GenerateSQL();
}
