using System.Linq.Expressions;

namespace Digital.Net.Lib.Predicates;

public static class PredicateBuilder
{
    /// <summary>
    ///     Returns a base predicate.
    /// </summary>
    public static Expression<Func<T, bool>> New<T>() => x => true;

    /// <summary>
    ///     Combines two predicates with an OR operator.
    /// </summary>
    /// <param name="expr1">The first predicate.</param>
    /// <param name="expr2">The second predicate.</param>
    /// <typeparam name="T">The type of the predicate.</typeparam>
    /// <returns>The combined predicate.</returns>
    public static Expression<Func<T, bool>> Add<T>(this Expression<Func<T, bool>> expr1,
        Expression<Func<T, bool>> expr2)
    {
        var invokedExpr = Expression.Invoke(expr2, expr1.Parameters);
        return Expression.Lambda<Func<T, bool>>(Expression.AndAlso(expr1.Body, invokedExpr), expr1.Parameters);
    }
}