using System.Globalization;
using System.Linq.Expressions;

namespace Nimblesite.Sql.Model;

/// <summary>
/// Expression visitor that builds SQL from LINQ expressions
/// </summary>
internal sealed class SelectStatementVisitor : ExpressionVisitor
{
    private readonly SelectStatementBuilder _builder;

    internal SelectStatementVisitor(SelectStatementBuilder builder)
    {
        _builder = builder;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        var method = node.Method;

        switch (method.Name)
        {
            case "Where":
                if (node.Arguments.Count >= 2)
                {
                    Visit(node.Arguments[0]);
                    var lambda = GetLambda(node.Arguments[1]);
                    if (lambda != null)
                    {
                        ProcessWhereExpression(lambda.Body);
                    }
                }
                return node;

            case "Select":
                if (node.Arguments.Count >= 2)
                {
                    Visit(node.Arguments[0]);
                    var lambda = GetLambda(node.Arguments[1]);
                    if (lambda != null)
                    {
                        ProcessSelectExpression(lambda.Body);
                    }
                }
                return node;

            case "OrderBy":
            case "ThenBy":
                if (node.Arguments.Count >= 2)
                {
                    Visit(node.Arguments[0]);
                    var lambda = GetLambda(node.Arguments[1]);
                    if (lambda != null && lambda.Body is MemberExpression member)
                    {
                        _builder.AddOrderBy(member.Member.Name, "ASC");
                    }
                }
                return node;

            case "OrderByDescending":
            case "ThenByDescending":
                if (node.Arguments.Count >= 2)
                {
                    Visit(node.Arguments[0]);
                    var lambda = GetLambda(node.Arguments[1]);
                    if (lambda != null && lambda.Body is MemberExpression member)
                    {
                        _builder.AddOrderBy(member.Member.Name, "DESC");
                    }
                }
                return node;

            case "Take":
                if (node.Arguments.Count >= 2)
                {
                    Visit(node.Arguments[0]);
                    if (node.Arguments[1] is ConstantExpression constant)
                    {
                        _builder.WithLimit(constant.Value?.ToString() ?? "0");
                    }
                }
                return node;

            case "Skip":
                if (node.Arguments.Count >= 2)
                {
                    Visit(node.Arguments[0]);
                    if (node.Arguments[1] is ConstantExpression constant)
                    {
                        _builder.WithOffset(constant.Value?.ToString() ?? "0");
                    }
                }
                return node;

            case "Distinct":
                Visit(node.Arguments[0]);
                _builder.WithDistinct();
                return node;

            case "GroupBy":
                if (node.Arguments.Count >= 2)
                {
                    Visit(node.Arguments[0]);
                    var lambda = GetLambda(node.Arguments[1]);
                    if (lambda != null)
                    {
                        var columns = ExtractColumns(lambda.Body);
                        _builder.AddGroupBy(columns);
                    }
                }
                return node;

            default:
                return base.VisitMethodCall(node);
        }
    }

    private static LambdaExpression? GetLambda(Expression expression) =>
        expression switch
        {
            UnaryExpression { Operand: LambdaExpression lambda } => lambda,
            LambdaExpression lambda => lambda,
            _ => null,
        };

    private void ProcessSelectExpression(Expression expression)
    {
        var columns = ExtractColumns(expression);
        foreach (var column in columns)
        {
            _builder.AddSelectColumn(column);
        }
    }

    private void ProcessWhereExpression(Expression expression)
    {
        // For complex expressions (like PredicateBuilder results), try to convert to single SQL expression
        var sqlExpression = TryConvertToSingleSqlExpression(expression);
        if (sqlExpression != null)
        {
            _builder.AddWhereCondition(WhereCondition.FromExpression(sqlExpression));
        }
        else
        {
            ProcessWhereExpressionRecursive(expression);
        }
    }

    private static string? TryConvertToSingleSqlExpression(Expression expression) =>
        expression switch
        {
            ConstantExpression constant when constant.Type == typeof(bool) => (bool)constant.Value!
                ? "1 = 1"
                : "1 = 0",

            UnaryExpression { NodeType: ExpressionType.Not } unary => ConvertExpressionToSql(unary),

            BinaryExpression binary when IsComplexPredicateBuilderExpression(binary) =>
                ConvertExpressionToSql(binary),

            _ => null,
        };

    private static bool IsComplexPredicateBuilderExpression(BinaryExpression binary) =>
        // Detect if this is a complex PredicateBuilder expression
        // PredicateBuilder expressions typically have constant boolean values at the leaf nodes
        (binary.NodeType is ExpressionType.AndAlso or ExpressionType.OrElse)
        && (HasDirectConstantBooleanNodes(binary) || IsPredicateBuilderPattern(binary));

    private static bool HasDirectConstantBooleanNodes(BinaryExpression binary) =>
        // Check for direct constant boolean nodes (immediate children)
        (binary.Left is ConstantExpression leftConst && leftConst.Type == typeof(bool))
        || (binary.Right is ConstantExpression rightConst && rightConst.Type == typeof(bool));

    private static bool IsPredicateBuilderPattern(BinaryExpression binary) =>
        // More sophisticated detection for PredicateBuilder patterns
        // Look for patterns like: (constant || expression) or (expression && constant)
        (
            binary.NodeType is ExpressionType.OrElse
            && (
                IsConstantBooleanOrPredicateBuilder(binary.Left)
                || IsConstantBooleanOrPredicateBuilder(binary.Right)
            )
        )
        || (
            binary.NodeType is ExpressionType.AndAlso
            && (
                IsConstantBooleanOrPredicateBuilder(binary.Left)
                || IsConstantBooleanOrPredicateBuilder(binary.Right)
            )
        );

    private static bool IsConstantBooleanOrPredicateBuilder(Expression expression) =>
        expression switch
        {
            ConstantExpression constant => constant.Type == typeof(bool),
            BinaryExpression binary => IsComplexPredicateBuilderExpression(binary),
            _ => false,
        };

    private static bool HasConstantBooleanNodes(Expression expression) =>
        expression switch
        {
            ConstantExpression constant => constant.Type == typeof(bool),
            BinaryExpression binary => HasConstantBooleanNodes(binary.Left)
                || HasConstantBooleanNodes(binary.Right),
            UnaryExpression unary => HasConstantBooleanNodes(unary.Operand),
            _ => false,
        };

    private static string ConvertExpressionToSql(Expression expression) =>
        expression switch
        {
            ConstantExpression constant when constant.Type == typeof(bool) => (bool)constant.Value!
                ? "1 = 1"
                : "1 = 0",

            UnaryExpression { NodeType: ExpressionType.Not } unary =>
                $"NOT ({ConvertExpressionToSql(unary.Operand)})",

            BinaryExpression binary when binary.NodeType == ExpressionType.AndAlso =>
                $"({ConvertExpressionToSql(binary.Left)}) AND ({ConvertExpressionToSql(binary.Right)})",

            BinaryExpression binary when binary.NodeType == ExpressionType.OrElse =>
                $"({ConvertExpressionToSql(binary.Left)}) OR ({ConvertExpressionToSql(binary.Right)})",

            BinaryExpression binary => ConvertComparisonToSql(binary),

            MemberExpression member when member.Type == typeof(bool) => $"{member.Member.Name} = 1",

            _ => "1 = 1", // Default fallback
        };

    private static string ConvertComparisonToSql(BinaryExpression binary)
    {
        var columnName = ExtractColumnName(binary.Left) ?? ExtractColumnName(binary.Right);
        var value = ExtractValue(binary.Right) ?? ExtractValue(binary.Left);

        // Handle NULL comparisons specially
        if (value == null)
        {
            return binary.NodeType switch
            {
                ExpressionType.Equal => $"{columnName} IS NULL",
                ExpressionType.NotEqual => $"{columnName} IS NOT NULL",
                _ => $"{columnName} IS NULL",
            };
        }

        var op = binary.NodeType switch
        {
            ExpressionType.Equal => "=",
            ExpressionType.NotEqual => "!=",
            ExpressionType.LessThan => "<",
            ExpressionType.LessThanOrEqual => "<=",
            ExpressionType.GreaterThan => ">",
            ExpressionType.GreaterThanOrEqual => ">=",
            _ => "=",
        };

        return $"{columnName} {op} {FormatValue(value)}";
    }

    private void ProcessWhereExpressionRecursive(Expression expression)
    {
        switch (expression)
        {
            case BinaryExpression binary:
                ProcessBinaryExpression(binary);
                break;

            case MethodCallExpression method:
                ProcessMethodCallInWhere(method);
                break;

            case UnaryExpression unary when unary.NodeType == ExpressionType.Not:
                // Handle NOT operation
                _builder.AddWhereCondition(WhereCondition.FromExpression("NOT ("));
                ProcessWhereExpressionRecursive(unary.Operand);
                _builder.AddWhereCondition(WhereCondition.FromExpression(")"));
                break;

            case MemberExpression member when member.Type == typeof(bool):
                _builder.AddWhereCondition(
                    WhereCondition.Comparison(
                        ColumnInfo.Named(member.Member.Name),
                        ComparisonOperator.Eq,
                        "1"
                    )
                );
                break;

            case ConstantExpression constant when constant.Type == typeof(bool):
                // Handle PredicateBuilder.True() and PredicateBuilder.False() constant expressions
                var value = (bool)constant.Value!;
                _builder.AddWhereCondition(
                    WhereCondition.FromExpression(value ? "1 = 1" : "1 = 0")
                );
                break;
        }
    }

    private void ProcessBinaryExpression(BinaryExpression binary)
    {
        switch (binary.NodeType)
        {
            case ExpressionType.AndAlso:
                // Special handling for constant boolean expressions
                if (
                    binary.Left is ConstantExpression leftConst
                    && leftConst.Type == typeof(bool)
                    && binary.Right is ConstantExpression rightConst
                    && rightConst.Type == typeof(bool)
                )
                {
                    // Optimize boolean constants: true AND true = true, false AND anything = false, etc.
                    var leftValue = (bool)leftConst.Value!;
                    var rightValue = (bool)rightConst.Value!;
                    var result = leftValue && rightValue;
                    _builder.AddWhereCondition(
                        WhereCondition.FromExpression(result ? "1 = 1" : "1 = 0")
                    );
                }
                else
                {
                    ProcessWhereExpressionRecursive(binary.Left);
                    _builder.AddWhereCondition(WhereCondition.And());
                    ProcessWhereExpressionRecursive(binary.Right);
                }
                break;

            case ExpressionType.OrElse:
                // Special handling for constant boolean expressions to avoid unnecessary parentheses
                if (
                    binary.Left is ConstantExpression orLeftConst
                    && orLeftConst.Type == typeof(bool)
                    && binary.Right is ConstantExpression orRightConst
                    && orRightConst.Type == typeof(bool)
                )
                {
                    // Optimize boolean constants: false OR false = false, true OR anything = true, etc.
                    var leftValue = (bool)orLeftConst.Value!;
                    var rightValue = (bool)orRightConst.Value!;
                    var result = leftValue || rightValue;
                    _builder.AddWhereCondition(
                        WhereCondition.FromExpression(result ? "1 = 1" : "1 = 0")
                    );
                }
                else
                {
                    _builder.AddWhereCondition(WhereCondition.OpenParen());
                    ProcessWhereExpressionRecursive(binary.Left);
                    _builder.AddWhereCondition(WhereCondition.CloseParen());
                    _builder.AddWhereCondition(WhereCondition.Or());
                    _builder.AddWhereCondition(WhereCondition.OpenParen());
                    ProcessWhereExpressionRecursive(binary.Right);
                    _builder.AddWhereCondition(WhereCondition.CloseParen());
                }
                break;

            case ExpressionType.Equal:
            case ExpressionType.NotEqual:
            case ExpressionType.LessThan:
            case ExpressionType.LessThanOrEqual:
            case ExpressionType.GreaterThan:
            case ExpressionType.GreaterThanOrEqual:
                AddComparisonCondition(binary);
                break;
        }
    }

    private void AddComparisonCondition(BinaryExpression binary)
    {
        var columnName = ExtractColumnName(binary.Left) ?? ExtractColumnName(binary.Right);
        var value = ExtractValue(binary.Right) ?? ExtractValue(binary.Left);

        if (columnName == null)
            return;

        // Handle NULL comparisons specially
        if (value == null)
        {
            var nullCondition = binary.NodeType switch
            {
                ExpressionType.Equal => $"{columnName} IS NULL",
                ExpressionType.NotEqual => $"{columnName} IS NOT NULL",
                _ => $"{columnName} IS NULL",
            };
            _builder.AddWhereCondition(WhereCondition.FromExpression(nullCondition));
            return;
        }

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

        _builder.AddWhereCondition(
            WhereCondition.Comparison(ColumnInfo.Named(columnName), op, FormatValue(value))
        );
    }

    private void ProcessMethodCallInWhere(MethodCallExpression method)
    {
        switch (method.Method.Name)
        {
            case "Contains" when method.Object != null:
                var columnName = ExtractColumnName(method.Object);
                var value = ExtractValue(method.Arguments[0]);
                if (columnName != null && value != null)
                {
                    _builder.AddWhereCondition(
                        WhereCondition.Comparison(
                            ColumnInfo.Named(columnName),
                            ComparisonOperator.Like,
                            $"%{value}%"
                        )
                    );
                }
                break;

            case "StartsWith" when method.Object != null:
                columnName = ExtractColumnName(method.Object);
                value = ExtractValue(method.Arguments[0]);
                if (columnName != null && value != null)
                {
                    _builder.AddWhereCondition(
                        WhereCondition.Comparison(
                            ColumnInfo.Named(columnName),
                            ComparisonOperator.Like,
                            $"{value}%"
                        )
                    );
                }
                break;
        }
    }

    private static IEnumerable<ColumnInfo> ExtractColumns(Expression expression) =>
        expression switch
        {
            MemberExpression member => [ColumnInfo.Named(member.Member.Name)],
            NewExpression newExpr => newExpr.Members?.Select(m => ColumnInfo.Named(m.Name)) ?? [],
            ParameterExpression => [ColumnInfo.Wildcard()],
            _ => [],
        };

    private static string? ExtractColumnName(Expression expression) =>
        expression switch
        {
            MemberExpression member => member.Member.Name,
            UnaryExpression unary => ExtractColumnName(unary.Operand),
            _ => null,
        };

    // Implements [MIG-AOT-DYNCODE]: walk the subtree to a constant instead of
    // Expression.Compile().DynamicInvoke() (IL3050, breaks Native AOT).
    private static object? ExtractValue(Expression expression) =>
        ConstantExpressionEvaluator.TryEvaluate(expression);

    private static string FormatValue(object? value) =>
        value switch
        {
            null => "NULL",
            string s => $"'{s.Replace("'", "''", StringComparison.Ordinal)}'",
            bool b => b ? "1" : "0",
            DateTime dt => $"'{dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}'",
            int i => i.ToString(CultureInfo.InvariantCulture),
            long l => l.ToString(CultureInfo.InvariantCulture),
            decimal d => d.ToString(CultureInfo.InvariantCulture),
            double db => db.ToString(CultureInfo.InvariantCulture),
            float f => f.ToString(CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "NULL",
        };
}
