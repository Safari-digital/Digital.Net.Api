using Digital.Net.Core.Http.Services.Pagination;
using Digital.Net.Lib.Abstractions;
using Digital.Net.Lib.Entities.Attributes;

namespace Digital.Net.Tests.Core.Http.Services.Pagination;

public class OrderByResolverTest : UnitTest
{
    private class Sample : IEntity
    {
        [Sortable]
        public Guid Id { get; set; }

        [Sortable]
        public DateTime CreatedAt { get; set; }

        [Sortable]
        public string Title { get; set; } = string.Empty;

        public string NotWhitelisted { get; set; } = string.Empty;

        [Sortable]
        [Secret]
        public string PasswordHash { get; set; } = string.Empty;
    }

    [Test]
    public async Task Resolve_ReturnsDefault_WhenNullOrBlank()
    {
        await Assert.That(OrderByResolver.Resolve<Sample>(null)).IsEqualTo(OrderByResolver.DefaultColumn);
        await Assert.That(OrderByResolver.Resolve<Sample>("  ")).IsEqualTo(OrderByResolver.DefaultColumn);
    }

    [Test]
    public async Task Resolve_ReturnsCanonicalName_WhenSortableColumn() =>
        await Assert.That(OrderByResolver.Resolve<Sample>("title")).IsEqualTo("Title");

    [Test]
    public async Task Resolve_Throws_WhenUnknownColumn()
    {
        await Assert.That(() => OrderByResolver.Resolve<Sample>("DROP TABLE")).Throws<InvalidOrderByException>();
        await Assert.That(() => OrderByResolver.Resolve<Sample>("NotAProperty")).Throws<InvalidOrderByException>();
    }

    [Test]
    public async Task Resolve_Throws_WhenColumnIsNotWhitelisted() => await Assert
        .That(() => OrderByResolver.Resolve<Sample>("NotWhitelisted")).Throws<InvalidOrderByException>();

    [Test]
    public async Task Resolve_Throws_WhenColumnIsSecret_EvenIfMarkedSortable() =>
        await Assert.That(() => OrderByResolver.Resolve<Sample>("PasswordHash")).Throws<InvalidOrderByException>();

    [Test]
    public async Task ResolveOrderClause_AppendsTieBreaker_ForStableTotalOrder()
    {
        await Assert.That(OrderByResolver.ResolveOrderClause<Sample>(null, null)).IsEqualTo("CreatedAt, Id");
        await Assert.That(OrderByResolver.ResolveOrderClause<Sample>("title", "desc"))
            .IsEqualTo("Title descending, Id");
    }

    [Test]
    public async Task ResolveOrderClause_DoesNotDuplicate_WhenOrderingByPrimaryKey()
    {
        await Assert.That(OrderByResolver.ResolveOrderClause<Sample>("id", null)).IsEqualTo("Id");
        await Assert.That(OrderByResolver.ResolveOrderClause<Sample>("Id", "desc")).IsEqualTo("Id descending");
    }
}
