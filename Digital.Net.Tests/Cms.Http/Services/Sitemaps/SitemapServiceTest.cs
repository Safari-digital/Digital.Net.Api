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
        _service = new SitemapService(_context, _context.BuildTemplateSourceResolvers());
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

    [Test]
    public async Task GetEntries_ShouldExpandDynamicArticlePage()
    {
        var prefix = "/" + Unique("art-pub");
        var pattern = $"{prefix}/:slug";
        var page = _context.BuildTestPage(path: pattern, published: true, indexed: true);

        var slugA = Unique("a");
        var slugB = Unique("b");
        _context.BuildTestArticle(slug: slugA, published: true, pageId: page.Id);
        _context.BuildTestArticle(slug: slugB, published: true, pageId: page.Id);

        var entries = await _service.GetEntriesAsync();

        await Assert.That(entries.Any(e => e.Path == $"{prefix}/{slugA}")).IsTrue();
        await Assert.That(entries.Any(e => e.Path == $"{prefix}/{slugB}")).IsTrue();
        await Assert.That(entries.Any(e => e.Path == pattern)).IsFalse();
    }

    [Test]
    public async Task GetEntries_ShouldExcludeUnpublishedArticlesFromDynamicExpansion()
    {
        var prefix = "/" + Unique("art-unp");
        var pattern = $"{prefix}/:slug";
        var page = _context.BuildTestPage(path: pattern, published: true, indexed: true);

        var draftSlug = Unique("draft");
        _context.BuildTestArticle(slug: draftSlug, published: false, pageId: page.Id);

        var entries = await _service.GetEntriesAsync();

        await Assert.That(entries.Any(e => e.Path == $"{prefix}/{draftSlug}")).IsFalse();
    }

    [Test]
    public async Task GetEntries_ShouldExcludeOrphanArticlesFromDynamicExpansion()
    {
        var prefix = "/" + Unique("art-orph");
        var pattern = $"{prefix}/:slug";
        _context.BuildTestPage(path: pattern, published: true, indexed: true);

        var orphanSlug = Unique("orph");
        _context.BuildTestArticle(slug: orphanSlug, published: true, pageId: null);

        var entries = await _service.GetEntriesAsync();

        await Assert.That(entries.Any(e => e.Path == $"{prefix}/{orphanSlug}")).IsFalse();
    }

    [Test]
    public async Task GetEntries_ShouldExcludeArticlesAttachedToOtherPage()
    {
        var prefixA = "/" + Unique("art-a");
        var prefixB = "/" + Unique("art-b");
        var patternA = $"{prefixA}/:slug";
        var patternB = $"{prefixB}/:slug";
        var pageA = _context.BuildTestPage(path: patternA, published: true, indexed: true);
        _context.BuildTestPage(path: patternB, published: true, indexed: true);

        var slug = Unique("only-a");
        _context.BuildTestArticle(slug: slug, published: true, pageId: pageA.Id);

        var entries = await _service.GetEntriesAsync();

        await Assert.That(entries.Any(e => e.Path == $"{prefixA}/{slug}")).IsTrue();
        await Assert.That(entries.Any(e => e.Path == $"{prefixB}/{slug}")).IsFalse();
    }

    [Test]
    public async Task GetEntries_ShouldNotExpandDynamicPage_WhenPageIsNotPublished()
    {
        var prefix = "/" + Unique("dyn-unp");
        var pattern = $"{prefix}/:slug";
        var page = _context.BuildTestPage(path: pattern, published: false, indexed: true);

        var slug = Unique("hidden");
        _context.BuildTestArticle(slug: slug, published: true, pageId: page.Id);

        var entries = await _service.GetEntriesAsync();

        await Assert.That(entries.Any(e => e.Path == $"{prefix}/{slug}")).IsFalse();
    }

    [Test]
    public async Task GetEntries_ShouldNotExpandDynamicPage_WhenPageIsNotIndexed()
    {
        var prefix = "/" + Unique("dyn-nox");
        var pattern = $"{prefix}/:slug";
        var page = _context.BuildTestPage(path: pattern, published: true, indexed: false);

        var slug = Unique("masked");
        _context.BuildTestArticle(slug: slug, published: true, pageId: page.Id);

        var entries = await _service.GetEntriesAsync();

        await Assert.That(entries.Any(e => e.Path == $"{prefix}/{slug}")).IsFalse();
    }

    [Test]
    public async Task GetEntries_ShouldUseMaxUpdatedAtBetweenPageAndArticle()
    {
        var prefix = "/" + Unique("upd");
        var pattern = $"{prefix}/:slug";
        var page = _context.BuildTestPage(path: pattern, published: true, indexed: true);

        var slug = Unique("entry");
        var article = _context.BuildTestArticle(slug: slug, published: true, pageId: page.Id);

        var pageDate = DateTime.UtcNow.AddDays(2);
        var articleDate = DateTime.UtcNow.AddDays(1);
        await _context.Pages.Where(p => p.Id == page.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.UpdatedAt, pageDate));
        await _context.Articles.Where(a => a.Id == article.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.UpdatedAt, articleDate));

        var entries = await _service.GetEntriesAsync();
        var entry = entries.FirstOrDefault(e => e.Path == $"{prefix}/{slug}");

        await Assert.That(entry).IsNotNull();
        await Assert.That(entry!.UpdatedAt > articleDate).IsTrue();
    }

    [Test]
    public async Task GetEntries_ShouldIgnoreDynamicPage_WhenNoSourceIsAttached()
    {
        var prefix = "/" + Unique("noent");
        var pattern = $"{prefix}/:slug";
        _context.BuildTestPage(path: pattern, published: true, indexed: true);

        var entries = await _service.GetEntriesAsync();

        await Assert.That(entries.Any(e => e.Path.StartsWith(prefix))).IsFalse();
    }

    [Test]
    public async Task GetEntries_ShouldPreferDedicatedPage_OverTemplateExpansion()
    {
        var prefix = "/" + Unique("dedup");
        var page = _context.BuildTestPage(path: $"{prefix}/:slug", published: true, indexed: true);
        var slug = Unique("art");
        _context.BuildTestArticle(slug: slug, published: true, pageId: page.Id);
        var dedicated = _context.BuildTestPage(path: $"{prefix}/{slug}", published: true, indexed: true);

        var sentinel = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await _context.Pages.Where(p => p.Id == dedicated.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.UpdatedAt, sentinel));

        var entries = await _service.GetEntriesAsync();
        var matches = entries.Where(e => e.Path == $"{prefix}/{slug}").ToList();

        await Assert.That(matches.Count).IsEqualTo(1);
        await Assert.That(matches[0].UpdatedAt).IsEqualTo(sentinel);
    }

    [Test]
    public async Task GetEntries_ShouldExcludeExpandedPath_WhenDedicatedPageNotIndexed()
    {
        var prefix = "/" + Unique("noidx");
        var page = _context.BuildTestPage(path: $"{prefix}/:slug", published: true, indexed: true);
        var slug = Unique("art");
        _context.BuildTestArticle(slug: slug, published: true, pageId: page.Id);
        _context.BuildTestPage(path: $"{prefix}/{slug}", published: true, indexed: false);

        var entries = await _service.GetEntriesAsync();

        await Assert.That(entries.Any(e => e.Path == $"{prefix}/{slug}")).IsFalse();
    }

    [Test]
    public async Task GetEntries_ShouldKeepExpandedPath_WhenDedicatedPageUnpublished()
    {
        var prefix = "/" + Unique("unpub");
        var page = _context.BuildTestPage(path: $"{prefix}/:slug", published: true, indexed: true);
        var slug = Unique("art");
        _context.BuildTestArticle(slug: slug, published: true, pageId: page.Id);
        _context.BuildTestPage(path: $"{prefix}/{slug}", published: false, indexed: true);

        var entries = await _service.GetEntriesAsync();

        await Assert.That(entries.Count(e => e.Path == $"{prefix}/{slug}")).IsEqualTo(1);
    }
}
