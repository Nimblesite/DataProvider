using System.Linq.Expressions;
using System.Reflection;

namespace Nimblesite.Sql.Model;

/// <summary>
/// Evaluates the constant value of a LINQ expression subtree without using
/// <see cref="LambdaExpression.Compile()"/>. Implements [MIG-AOT-DYNCODE]:
/// <c>Expression.Lambda(expr).Compile().DynamicInvoke()</c> carries
/// <see cref="System.Diagnostics.CodeAnalysis.RequiresDynamicCodeAttribute"/>
/// (IL3050) and breaks Native AOT. The cases that occur when translating a LINQ
/// predicate to SQL — literals, captured locals/fields, static fields/properties,
/// and member chains over those — are all walkable without emitting code.
/// </summary>
internal static class ConstantExpressionEvaluator
{
    /// <summary>
    /// Returns the evaluated value of <paramref name="expression"/>, or
    /// <c>null</c> when the subtree cannot be reduced to a value without dynamic
    /// code (matching the prior <c>catch</c> fallback behaviour).
    /// </summary>
    internal static object? TryEvaluate(Expression expression) =>
        expression switch
        {
            ConstantExpression constant => constant.Value,
            MemberExpression member => EvaluateMember(member),
            UnaryExpression { NodeType: ExpressionType.Convert } convert => TryEvaluate(
                convert.Operand
            ),
            UnaryExpression { NodeType: ExpressionType.ConvertChecked } convert => TryEvaluate(
                convert.Operand
            ),
            _ => null,
        };

    private static object? EvaluateMember(MemberExpression member)
    {
        // A static member (member.Expression is null) or a member over an already
        // reducible subtree (a captured closure constant, a nested field, etc.).
        var instance = member.Expression is null ? null : TryEvaluate(member.Expression);

        if (member.Expression is not null && instance is null)
        {
            return null;
        }

        return member.Member switch
        {
            FieldInfo field => field.GetValue(instance),
            PropertyInfo property when property.GetIndexParameters().Length == 0 =>
                property.GetValue(instance),
            _ => null,
        };
    }
}
