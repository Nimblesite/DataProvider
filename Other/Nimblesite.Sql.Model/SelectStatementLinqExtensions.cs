using System.Data;
using System.Globalization;
using System.Linq.Expressions;

namespace Nimblesite.Sql.Model;

/// <summary>
/// LINQ expression extensions for building SQL statements
/// </summary>
public static class SelectStatementLinqExtensions
{
    /// <summary>
    /// Creates a new SELECT query from a table name
    /// </summary>
    public static SelectStatementBuilder From(this string tableName, string? alias = null) =>
        new SelectStatementBuilder().AddTable(tableName, alias);

    /// <summary>
    /// Adds SELECT columns using a projection expression
    /// </summary>
    public static SelectStatementBuilder Select<T>(
        this SelectStatementBuilder builder,
        Expression<Func<T, object>> selector
    )
    {
        var columns = ExtractColumns(selector);
        foreach (var column in columns)
        {
            builder.AddSelectColumn(column);
        }
        return builder;
    }

    /// <summary>
    /// Adds SELECT columns using table alias and column name pairs
    /// </summary>
    public static SelectStatementBuilder Select(
        this SelectStatementBuilder builder,
        params (string? tableAlias, string columnName)[] columns
    )
    {
        foreach (var (tableAlias, columnName) in columns)
        {
            builder.AddSelectColumn(columnName, alias: null, tableAlias: tableAlias);
        }
        return builder;
    }

    /// <summary>
    /// Adds SELECT * (all columns)
    /// </summary>
    public static SelectStatementBuilder SelectAll(
        this SelectStatementBuilder builder,
        string? tableAlias = null
    ) => builder.AddSelectColumn(ColumnInfo.Wildcard(tableAlias));

    /// <summary>
    /// Adds a WHERE condition using an expression
    /// </summary>
    public static SelectStatementBuilder Where<T>(
        this SelectStatementBuilder builder,
        Expression<Func<T, bool>> predicate
    )
    {
        var conditions = ExtractWhereConditions(predicate);
        foreach (var condition in conditions)
        {
            builder.AddWhereCondition(condition);
        }
        return builder;
    }

    /// <summary>
    /// Adds a WHERE condition using a simple equality check
    /// </summary>
    public static SelectStatementBuilder Where(
        this SelectStatementBuilder builder,
        string columnName,
        object value
    ) =>
        builder.AddWhereCondition(
            WhereCondition.Comparison(
                ColumnInfo.Named(columnName),
                ComparisonOperator.Eq,
                FormatValue(value)
            )
        );

    /// <summary>
    /// Adds a WHERE condition with a comparison operator
    /// </summary>
    public static SelectStatementBuilder Where(
        this SelectStatementBuilder builder,
        string columnName,
        ComparisonOperator @operator,
        object value
    ) =>
        builder.AddWhereCondition(
            WhereCondition.Comparison(ColumnInfo.Named(columnName), @operator, FormatValue(value))
        );

    /// <summary>
    /// Adds an AND condition
    /// </summary>
    public static SelectStatementBuilder And(
        this SelectStatementBuilder builder,
        string columnName,
        object value
    ) =>
        builder
            .AddWhereCondition(WhereCondition.And())
            .AddWhereCondition(
                WhereCondition.Comparison(
                    ColumnInfo.Named(columnName),
                    ComparisonOperator.Eq,
                    FormatValue(value)
                )
            );

    /// <summary>
    /// Adds an OR condition
    /// </summary>
    public static SelectStatementBuilder Or(
        this SelectStatementBuilder builder,
        string columnName,
        object value
    ) =>
        builder
            .AddWhereCondition(WhereCondition.Or())
            .AddWhereCondition(
                WhereCondition.Comparison(
                    ColumnInfo.Named(columnName),
                    ComparisonOperator.Eq,
                    FormatValue(value)
                )
            );

    /// <summary>
    /// Adds an INNER JOIN
    /// </summary>
    public static SelectStatementBuilder InnerJoin(
        this SelectStatementBuilder builder,
        string rightTable,
        string leftColumn,
        string rightColumn,
        string? leftTableAlias = null,
        string? rightTableAlias = null
    )
    {
        var leftRef = leftTableAlias != null ? $"{leftTableAlias}.{leftColumn}" : leftColumn;
        var rightRef = rightTableAlias != null ? $"{rightTableAlias}.{rightColumn}" : rightColumn;
        var condition = $"{leftRef} = {rightRef}";

        // Add the right table to the tables collection
        builder.AddTable(rightTable, rightTableAlias);

        return builder.AddJoin(
            leftTableAlias ?? builder.GetFirstTableName(),
            rightTable,
            condition,
            "INNER"
        );
    }

    /// <summary>
    /// Adds a LEFT JOIN
    /// </summary>
    public static SelectStatementBuilder LeftJoin(
        this SelectStatementBuilder builder,
        string rightTable,
        string leftColumn,
        string rightColumn,
        string? leftTableAlias = null,
        string? rightTableAlias = null
    )
    {
        var leftRef = leftTableAlias != null ? $"{leftTableAlias}.{leftColumn}" : leftColumn;
        var rightRef = rightTableAlias != null ? $"{rightTableAlias}.{rightColumn}" : rightColumn;
        var condition = $"{leftRef} = {rightRef}";

        // Add the right table to the tables collection
        builder.AddTable(rightTable, rightTableAlias);

        return builder.AddJoin(
            leftTableAlias ?? builder.GetFirstTableName(),
            rightTable,
            condition,
            "LEFT"
        );
    }

    /// <summary>
    /// Adds ORDER BY ascending
    /// </summary>
    public static SelectStatementBuilder OrderBy(
        this SelectStatementBuilder builder,
        string columnName
    ) => builder.AddOrderBy(columnName, "ASC");

    /// <summary>
    /// Adds ORDER BY descending
    /// </summary>
    public static SelectStatementBuilder OrderByDescending(
        this SelectStatementBuilder builder,
        string columnName
    ) => builder.AddOrderBy(columnName, "DESC");

    /// <summary>
    /// Adds multiple ORDER BY columns using expressions
    /// </summary>
    public static SelectStatementBuilder OrderBy<T>(
        this SelectStatementBuilder builder,
        Expression<Func<T, object>> selector
    )
    {
        var columns = ExtractColumns(selector);
        foreach (var column in columns)
        {
            if (column is NamedColumn named)
            {
                builder.AddOrderBy(named.Name, "ASC");
            }
        }
        return builder;
    }

    /// <summary>
    /// Adds GROUP BY columns
    /// </summary>
    public static SelectStatementBuilder GroupBy(
        this SelectStatementBuilder builder,
        params string[] columnNames
    ) => builder.AddGroupBy(columnNames.Select(c => ColumnInfo.Named(c)));

    /// <summary>
    /// Sets the LIMIT value
    /// </summary>
    public static SelectStatementBuilder Take(this SelectStatementBuilder builder, int count) =>
        builder.WithLimit(count.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Sets the OFFSET value
    /// </summary>
    public static SelectStatementBuilder Skip(this SelectStatementBuilder builder, int count) =>
        builder.WithOffset(count.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Sets DISTINCT
    /// </summary>
    public static SelectStatementBuilder Distinct(this SelectStatementBuilder builder) =>
        builder.WithDistinct();

    /// <summary>
    /// Builds the final SelectStatement
    /// </summary>
    public static SelectStatement ToSqlStatement(this SelectStatementBuilder builder) =>
        builder.Build();

    // Helper methods

    private static string GetFirstTableName(this SelectStatementBuilder _) =>
        // Since we can't access private fields, we'll need to track this separately
        // or modify the builder to expose this information
        // For now, returning empty string - you may want to enhance this
        string.Empty;

    private static IEnumerable<ColumnInfo> ExtractColumns<T>(Expression<Func<T, object>> selector)
    {
        var body = selector.Body;

        // Handle Convert/ConvertChecked for value types
        if (
            body is UnaryExpression unary
            && (
                unary.NodeType == ExpressionType.Convert
                || unary.NodeType == ExpressionType.ConvertChecked
            )
        )
        {
            body = unary.Operand;
        }

        return body switch
        {
            MemberExpression member => [ColumnInfo.Named(member.Member.Name)],
            NewExpression newExpr => newExpr.Members?.Select(m => ColumnInfo.Named(m.Name)) ?? [],
            _ => [],
        };
    }

    private static List<WhereCondition> ExtractWhereConditions<T>(
        Expression<Func<T, bool>> predicate
    )
    {
        var conditions = new List<WhereCondition>();
        ProcessExpression(predicate.Body, conditions);
        return conditions;
    }

    private static void ProcessExpression(Expression expr, List<WhereCondition> conditions)
    {
        switch (expr)
        {
            case BinaryExpression binary:
                ProcessBinaryExpression(binary, conditions);
                break;
            case UnaryExpression unary when unary.NodeType == ExpressionType.Not:
                // Handle NOT expressions if needed
                break;
            case MethodCallExpression method:
                ProcessMethodCall(method, conditions);
                break;
        }
    }

    private static void ProcessBinaryExpression(
        BinaryExpression binary,
        List<WhereCondition> conditions
    )
    {
        switch (binary.NodeType)
        {
            case ExpressionType.AndAlso:
                ProcessExpression(binary.Left, conditions);
                conditions.Add(WhereCondition.And());
                ProcessExpression(binary.Right, conditions);
                break;

            case ExpressionType.OrElse:
                conditions.Add(WhereCondition.OpenParen());
                ProcessExpression(binary.Left, conditions);
                conditions.Add(WhereCondition.CloseParen());
                conditions.Add(WhereCondition.Or());
                conditions.Add(WhereCondition.OpenParen());
                ProcessExpression(binary.Right, conditions);
                conditions.Add(WhereCondition.CloseParen());
                break;

            case ExpressionType.Equal:
            case ExpressionType.NotEqual:
            case ExpressionType.LessThan:
            case ExpressionType.LessThanOrEqual:
            case ExpressionType.GreaterThan:
            case ExpressionType.GreaterThanOrEqual:
                AddComparisonCondition(binary, conditions);
                break;
        }
    }

    private static void AddComparisonCondition(
        BinaryExpression binary,
        List<WhereCondition> conditions
    )
    {
        var columnName = ExtractColumnName(binary.Left);
        var value = ExtractValue(binary.Right);
        var op = binary.NodeType switch
        {
            ExpressionType.Equal => ComparisonOperator.Eq,
            ExpressionType.NotEqual => ComparisonOperator.NotEq,
            ExpressionType.LessThan => ComparisonOperator.LessThan,
            ExpressionType.LessThanOrEqual => ComparisonOperator.LessOrEq,
            ExpressionType.GreaterThan => ComparisonOperator.GreaterThan,
            ExpressionType.GreaterThanOrEqual => ComparisonOperator.GreaterOrEq,
            _ => ComparisonOperator.Eq,
        };

        if (columnName != null && value != null)
        {
            conditions.Add(
                WhereCondition.Comparison(ColumnInfo.Named(columnName), op, FormatValue(value))
            );
        }
    }

    private static void ProcessMethodCall(
        MethodCallExpression method,
        List<WhereCondition> conditions
    )
    {
        // Handle common string methods
        if (method.Method.Name == "Contains" && method.Object != null)
        {
            var columnName = ExtractColumnName(method.Object);
            var value = ExtractValue(method.Arguments[0]);
            if (columnName != null && value != null)
            {
                conditions.Add(
                    WhereCondition.Comparison(
                        ColumnInfo.Named(columnName),
                        ComparisonOperator.Like,
                        $"%{value}%"
                    )
                );
            }
        }
        else if (method.Method.Name == "StartsWith" && method.Object != null)
        {
            var columnName = ExtractColumnName(method.Object);
            var value = ExtractValue(method.Arguments[0]);
            if (columnName != null && value != null)
            {
                conditions.Add(
                    WhereCondition.Comparison(
                        ColumnInfo.Named(columnName),
                        ComparisonOperator.Like,
                        $"{value}%"
                    )
                );
            }
        }
    }

    private static string? ExtractColumnName(Expression expr) =>
        expr switch
        {
            MemberExpression member => member.Member.Name,
            UnaryExpression unary => ExtractColumnName(unary.Operand),
            _ => null,
        };

    // Implements [MIG-AOT-DYNCODE]: walk the subtree to a constant instead of
    // Expression.Compile().DynamicInvoke() (IL3050, breaks Native AOT).
    private static object? ExtractValue(Expression expr) =>
        ConstantExpressionEvaluator.TryEvaluate(expr);

    private static string FormatValue(object? value) =>
        value switch
        {
            null => "NULL",
            string s => $"'{s.Replace("'", "''", StringComparison.Ordinal)}'",
            bool b => b ? "1" : "0",
            DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
            _ => value.ToString() ?? "NULL",
        };
}
