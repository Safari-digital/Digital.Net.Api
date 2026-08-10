using Digital.Net.Cms.Context;
using Digital.Net.Cms.Http.Services;
using Digital.Net.Cms.Models.Pages;
using Digital.Net.Tests.Core;
using Digital.Net.Tests.Core.Factories;
using Digital.Net.Tests.Core.Factories.Data;
using Microsoft.EntityFrameworkCore;
using TUnit.Core.Interfaces;

namespace Digital.Net.Tests.Cms.Http.Services.Sitemaps;

public class SitemapServiceTest : UnitTest, IAsyncInitializer
{
    [ClassDataSource<DatabaseFixture>]
    public required DatabaseFixture DbFixture { get; init; }

    private CmsContext _context = null!;
    private SitemapService _service = null!;

    public async Task InitializeAsync()
    {
        await DbFixture.EnsureCreatedAsync<CmsContext>();
        _context = DbFixture.CreateContext<CmsContext>();
        _service = new SitemapService(_context);
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..(prefix.Length + 9)];

    [Test]
    public async Task GetEntries_ShouldIncludeStaticPublishedAndIndexedPage()
    {
        var path = "/" + Unique("static");
        _context.BuildTestPage(path: path, published: true, indexed: true);

        var entries = await _service.GetEntriesAsync();

        await Assert.That(entries.Any(e => e.Path == path)).IsTrue();
    }

    [Test]
    public async Task GetEntries_ShouldExcludeUnpublishedPage()
    {
        var path = "/" + Unique("unpub");
        _context.BuildTestPage(path: path, published: false, indexed: true);

        var entries = await _service.GetEntriesAsync();

        await Assert.That(entries.Any(e => e.Path == path)).IsFalse();
    }

    [Test]
    public async Task GetEntries_ShouldExcludeNonIndexedPage()
    {
        var path = "/" + Unique("noindex");
        _context.BuildTestPage(path: path, published: true, indexed: false);

        var entries = await _service.GetEntriesAsync();

        await Assert.That(entries.Any(e => e.Path == path)).IsFalse();
    }

}
