using System.Text.Json;
using Digital.Net.Cms.Context;
using Digital.Net.Cms.Http.Dto;
using Digital.Net.Cms.Http.Services;
using Digital.Net.Cms.Models;
using Digital.Net.Cms.Models.Pages;
using Digital.Net.Core.Http.Services.Crud;
using Digital.Net.Lib.Accessors;
using Digital.Net.Lib.Entities.Context;
using Digital.Net.Lib.Entities.Interceptors;
using Digital.Net.Lib.Entities.Mutations;
using Digital.Net.Lib.Entities.Pivots;
using Digital.Net.Tests.Core;
using Digital.Net.Tests.Core.Factories;
using Digital.Net.Tests.Core.Factories.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Core.Interfaces;

namespace Digital.Net.Tests.Cms.Http.Services.Pages;

/// <summary>
///     US-MUT-05 parent-touch: a patch that only changes sub-content/pivots (e.g. openGraph) must still emit a
///     mutation on the parent <c>Page</c> so its cache is invalidated.
/// </summary>
public class MutationParentTouchTest : UnitTest, IAsyncInitializer
{
    [ClassDataSource<DatabaseFixture>]
    public required DatabaseFixture DbFixture { get; init; }

    private CmsContext _context = null!;
    private PageCrudService _service = null!;

    public async Task InitializeAsync()
    {
        await DbFixture.EnsureCreatedAsync<CmsContext>();
        _context = CreateTrackedCmsContext();

        var openGraphResolver = new PivotPatchResolver<
            CmsContext, Page, OpenGraphEntry, PageOpenGraph, OpenGraphEntryPayloadDto
        >(_context);
        var dispatcher = new PatchDispatcher<Page>([openGraphResolver]);
        var crudService = new CrudService<CmsContext, Page>(_context, dispatcher);
        _service = new PageCrudService(_context, crudService);
    }

    [Test]
    public async Task PatchPage_OpenGraphOnly_EmitsParentPageMutation()
    {
        var page = _context.BuildTestPage();
        var patch = JsonSerializer.SerializeToElement(new object[]
        {
            new { op = "replace", path = "/openGraph", value = new[] { new { property = "og:title", content = "X" } } }
        });

        var result = await _service.PatchPage(patch, page.Id, Guid.NewGuid());
        await Assert.That(result.HasError).IsFalse();

        await using var read = DbFixture.CreateContext<CmsContext>();
        var pageUpdates = await read.Set<EntityMutation>()
            .CountAsync(m => m.EntityId == page.Id
                             && m.EntityType == nameof(Page)
                             && m.ChangeType == ChangeType.Updated);
        await Assert.That(pageUpdates).IsGreaterThanOrEqualTo(1);
    }

    private CmsContext CreateTrackedCmsContext()
    {
        var provider = new ServiceCollection()
            .AddSingleton<IOriginAccessor>(new StubOriginAccessor())
            .AddSingleton<IUserAccessor>(new StubUserAccessor())
            .AddSingleton(new MutationBroadcaster(NullLogger<MutationBroadcaster>.Instance))
            .BuildServiceProvider();

        var options = new DbContextOptionsBuilder<CmsContext>()
            .UseDigitalNpgsql<CmsContext>(DbFixture.Fixture.ConnectionString)
            .AddInterceptors(new MutationTrackingInterceptor(provider))
            .Options;

        return (CmsContext)Activator.CreateInstance(typeof(CmsContext), options)!;
    }

    private sealed class StubOriginAccessor : IOriginAccessor
    {
        public RequestOrigin GetOrigin() => new(null, null);
        public RequestOrigin TryGetOrigin() => new(null, null);
    }

    private sealed class StubUserAccessor : IUserAccessor
    {
        public Guid GetUserId() => throw new InvalidOperationException();
        public Guid? TryGetUserId() => null;
    }
}
