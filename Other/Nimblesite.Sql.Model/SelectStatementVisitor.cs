using System.Globalization;
using System.Linq.Expressions;

namespace Nimblesite.Sql.Model;

/// <summary>
/// Expression visitor that builds SQL from LINQ expressions
/// </summary>
internal sealed class SelectStatementVisitor : ExpressionVisitor
{
    private readonly SelectStatementBuilder _builder;

    internal SelectStatementVisitor(SelectStatementBuilder builder) => _builder = builder;

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        var method = node.Method;

        switch (method.Name)
        {
            case "Where":
                return VisitSourceAndProcessLambda(node, ProcessWhereExpression);

            case "Select":
                return VisitSourceAndProcessLambda(node, ProcessSelectExpression);

            case "OrderBy":
            case "ThenBy":
                return AddOrderByFromLambda(node, "ASC");

            case "OrderByDescending":
            case "ThenByDescending":
                return AddOrderByFromLambda(node, "DESC");

            case "Take":
                return ApplyRowCountConstraint(node, limit => _builder.WithLimit(limit));
            case "Skip":
                return ApplyRowCountConstraint(node, offset => _builder.WithOffset(offset));

            case "Distinct":
                Visit(node.Arguments[0]);
                _builder.WithDistinct();
                return node;

            case "GroupBy":
                return VisitSourceAndProcessLambda(
                    node,
                    body => _builder.AddGroupBy(ExtractColumns(body))
                );

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

    private MethodCallExpression VisitSourceAndProcessLambda(
        MethodCallExpression node,
        Action<Expression> processBody
    )
    {
        if (node.Arguments.Count >= 2)
        {
            Visit(node.Arguments[0]);
            var lambda = GetLambda(node.Arguments[1]);
            if (lambda != null)
            {
                processBody(lambda.Body);
            }
        }
        return node;
    }

    private MethodCallExpression AddOrderByFromLambda(MethodCallExpression node, string direction)
    {
        if (node.Arguments.Count >= 2)
        {
            Visit(node.Arguments[0]);
            var lambda = GetLambda(node.Arguments[1]);
            if (lambda != null && lambda.Body is MemberExpression member)
            {
                _builder.AddOrderBy(member.Member.Name, direction);
            }
        }
        return node;
    }

    private MethodCallExpression ApplyRowCountConstraint(
        MethodCallExpression node,
        Action<string> apply
    )
    {
        if (node.Arguments.Count >= 2)
        {
            Visit(node.Arguments[0]);
            if (node.Arguments[1] is ConstantExpression constant)
            {
                apply(constant.Value?.ToString() ?? "0");
            }
        }
        return node;
    }

    private static string? TryFoldBooleanConstants(
        BinaryExpression binary,
        Func<bool, bool, bool> combine
    ) =>
        binary.Left is ConstantExpression { Value: bool left } leftConst
        && leftConst.Type == typeof(bool)
        && binary.Right is ConstantExpression { Value: bool right } rightConst
        && rightConst.Type == typeof(bool)
            ? BooleanSql(combine(left, right))
            : null;

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
            ConstantExpression constant
                when constant.Type == typeof(bool) && constant.Value is bool boolValue =>
                BooleanSql(boolValue),

            UnaryExpression { NodeType: ExpressionType.Not } unary => ConvertExpressionToSql(unary),

            BinaryExpression binary when IsComplexPredicateBuilderExpression(binary) =>
                ConvertExpressionToSql(binary),

            _ => null,
        };

    private static bool IsComplexPredicateBuilderExpression(BinaryExpression binary) =>
        // Detect if this is a complex PredicateBuilder expression
        // PredicateBuilder expressions typically have constant boolean values at the leaf nodes
        binary.NodeType
            is ExpressionType.AndAlso
                or ExpressionType.OrElse
        && (
            HasDirectConstantBooleanNodes(binary)
            || IsConstantBooleanOrPredicateBuilder(binary.Left)
            || IsConstantBooleanOrPredicateBuilder(binary.Right)
        );

    private static bool HasDirectConstantBooleanNodes(BinaryExpression binary) =>
        // Check for direct constant boolean nodes (immediate children)
        (binary.Left is ConstantExpression leftConst && leftConst.Type == typeof(bool))
        || (binary.Right is ConstantExpression rightConst && rightConst.Type == typeof(bool));

    private static bool IsConstantBooleanOrPredicateBuilder(Expression expression) =>
        expression switch
        {
            ConstantExpression constant => constant.Type == typeof(bool),
            BinaryExpression binary => IsComplexPredicateBuilderExpression(binary),
            _ => false,
        };

    private static string ConvertExpressionToSql(Expression expression) =>
        expression switch
        {
            ConstantExpression constant
                when constant.Type == typeof(bool) && constant.Value is bool boolValue =>
                BooleanSql(boolValue),

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
        var (columnName, value) = ExtractComparisonOperands(binary);

        // Handle NULL comparisons specially
        if (value == null)
        {
            return NullComparisonSql(columnName, binary.NodeType);
        }

        var op = ComparisonOperatorFor(binary.NodeType).ToSql();

        return $"{columnName} {op} {FormatValue(value, CultureInfo.InvariantCulture)}";
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

            case ConstantExpression { Value: bool value } constant
                when constant.Type == typeof(bool):
                // Handle PredicateBuilder.True() and PredicateBuilder.False() constant expressions
                _builder.AddWhereCondition(WhereCondition.FromExpression(BooleanSql(value)));
                break;
        }
    }

    private void ProcessBinaryExpression(BinaryExpression binary)
    {
        switch (binary.NodeType)
        {
            case ExpressionType.AndAlso:
                // Special handling for constant boolean expressions
                var andFolded = TryFoldBooleanConstants(binary, static (l, r) => l && r);
                if (andFolded != null)
                {
                    _builder.AddWhereCondition(WhereCondition.FromExpression(andFolded));
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
                var orFolded = TryFoldBooleanConstants(binary, static (l, r) => l || r);
                if (orFolded != null)
                {
                    _builder.AddWhereCondition(WhereCondition.FromExpression(orFolded));
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
        var (columnName, value) = ExtractComparisonOperands(binary);

        if (columnName == null)
            return;

        // Handle NULL comparisons specially
        if (value == null)
        {
            _builder.AddWhereCondition(
                WhereCondition.FromExpression(NullComparisonSql(columnName, binary.NodeType))
            );
            return;
        }

        _builder.AddWhereCondition(
            WhereCondition.Comparison(
                ColumnInfo.Named(columnName),
                ComparisonOperatorFor(binary.NodeType),
                FormatValue(value, CultureInfo.InvariantCulture)
            )
        );
    }

    private void ProcessMethodCallInWhere(MethodCallExpression method)
    {
        switch (method.Method.Name)
        {
            case "Contains" when method.Object != null:
                var value = ExtractValue(method.Arguments[0]);
                AddLikeCondition(ExtractColumnName(method.Object), value, $"%{value}%");
                break;

            case "StartsWith" when method.Object != null:
                value = ExtractValue(method.Arguments[0]);
                AddLikeCondition(ExtractColumnName(method.Object), value, $"{value}%");
                break;
        }
    }

    private void AddLikeCondition(string? columnName, object? value, string pattern)
    {
        if (columnName != null && value != null)
        {
            _builder.AddWhereCondition(
                WhereCondition.Comparison(
                    ColumnInfo.Named(columnName),
                    ComparisonOperator.Like,
                    pattern
                )
            );
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

    internal static string? ExtractColumnName(Expression expression) =>
        expression switch
        {
            MemberExpression member => member.Member.Name,
            UnaryExpression unary => ExtractColumnName(unary.Operand),
            _ => null,
        };

    // Implements [MIG-AOT-DYNCODE]: walk the subtree to a constant instead of
    // Expression.Compile().DynamicInvoke() (IL3050, breaks Native AOT).
    internal static object? ExtractValue(Expression expression) =>
        ConstantExpressionEvaluator.TryEvaluate(expression);

    internal static ComparisonOperator ComparisonOperatorFor(ExpressionType nodeType) =>
        nodeType switch
        {
            ExpressionType.NotEqual => ComparisonOperator.NotEq,
            ExpressionType.LessThan => ComparisonOperator.LessThan,
            ExpressionType.LessThanOrEqual => ComparisonOperator.LessOrEq,
            ExpressionType.GreaterThan => ComparisonOperator.GreaterThan,
            ExpressionType.GreaterThanOrEqual => ComparisonOperator.GreaterOrEq,
            _ => ComparisonOperator.Eq,
        };

    private static string NullComparisonSql(string? columnName, ExpressionType nodeType) =>
        nodeType switch
        {
            ExpressionType.NotEqual => $"{columnName} IS NOT NULL",
            _ => $"{columnName} IS NULL",
        };

    private static (string? ColumnName, object? Value) ExtractComparisonOperands(
        BinaryExpression binary
    ) =>
        (
            ExtractColumnName(binary.Left) ?? ExtractColumnName(binary.Right),
            ExtractValue(binary.Right) ?? ExtractValue(binary.Left)
        );

    private static string BooleanSql(bool value) => value ? "1 = 1" : "1 = 0";

    // null formatProvider = current culture (LINQ extensions); InvariantCulture = SQL literals (visitor)
    internal static string FormatValue(object? value, IFormatProvider? formatProvider) =>
        value switch
        {
            null => "NULL",
            string s => $"'{s.Replace("'", "''", StringComparison.Ordinal)}'",
            bool b => b ? "1" : "0",
            DateTime dt => $"'{dt.ToString("yyyy-MM-dd HH:mm:ss", formatProvider)}'",
            int i => i.ToString(formatProvider),
            long l => l.ToString(formatProvider),
            decimal d => d.ToString(formatProvider),
            double db => db.ToString(formatProvider),
            float f => f.ToString(formatProvider),
            _ => value.ToString() ?? "NULL",
        };
}
