using System.Linq.Expressions;
using Digital.Net.Lib.Predicates;
using Digital.Net.Tests.Core;

namespace Digital.Net.Tests.Lib.Predicates;

public class PredicateBuilderTest : UnitTest
{
    [Test]
    public async Task New_ReturnsCorrectExpression()
    {
        var expression = PredicateBuilder.New<int>();
        var compiledExpression = expression.Compile();
        await Assert.That(compiledExpression(0)).IsTrue();
    }

    [Test]
    public async Task And_ReturnsCombinedExpression()
    {
        Expression<Func<int, bool>> expr1 = x => x > 5;
        Expression<Func<int, bool>> expr2 = x => x < 10;
        var combinedExpression = expr1.And(expr2);
        var compiledExpression = combinedExpression.Compile();
        await Assert.That(compiledExpression(7)).IsTrue();
        await Assert.That(compiledExpression(4)).IsFalse();
        await Assert.That(compiledExpression(11)).IsFalse();
    }
}