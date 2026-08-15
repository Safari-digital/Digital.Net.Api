using System.Linq.Expressions;

namespace Digital.Net.Lib.Predicates;

public static class PredicateBuilder
{
    /// <summary>
    ///     Returns a base predicate.
    /// </summary>
    public static Expression<Func<T, bool>> New<T>() => x => true;

    /// <summary>
    ///     Combines two predicates with an AND operator.
    /// </summary>
    /// <param name="left">The first predicate.</param>
    /// <param name="right">The second predicate.</param>
    /// <typeparam name="T"></typeparam>
    public static Expression<Func<T, bool>> And<T>(this Expression<Func<T, bool>> left, Expression<Func<T, bool>> right)
    {
        var parameter = left.Parameters[0];
        var rightBody = new ReplaceParameterVisitor(right.Parameters[0], parameter).Visit(right.Body);
        return Expression.Lambda<Func<T, bool>>(Expression.AndAlso(left.Body, rightBody), parameter);
    }

    private sealed class ReplaceParameterVisitor(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == from ? to : node;
        }
    }
}