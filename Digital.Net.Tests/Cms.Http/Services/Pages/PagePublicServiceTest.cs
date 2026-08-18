using Digital.Net.Cms.Context;
using Digital.Net.Cms.Http.Dto;
using Digital.Net.Cms.Http.Exceptions;
using Digital.Net.Cms.Http.Services;
using Digital.Net.Cms.Models;
using Digital.Net.Cms.Models.Pages;
using Digital.Net.Lib.Entities.Templating;
using Digital.Net.Lib.Templating.Models;
using Digital.Net.Tests.Core;
using Digital.Net.Tests.Core.Factories;
using Digital.Net.Tests.Core.Factories.Data;
using Digital.Net.Tests.Lib.Entities.Templating;
using TUnit.Core.Interfaces;

namespace Digital.Net.Tests.Cms.Http.Services.Pages;

public class PagePublicServiceTest : UnitTest, IAsyncInitializer
{
    [ClassDataSource<DatabaseFixture>]
    public required DatabaseFixture DbFixture { get; init; }
    
    private CmsContext _context = null!;
    private TemplatingTestContext _sourceContext = null!;
    private PagePublicService _service = null!;

    public async Task InitializeAsync()
    {
        await DbFixture.EnsureCreatedAsync<CmsContext>();
        await DbFixture.EnsureCreatedAsync<TemplatingTestContext>();
        _context = DbFixture.CreateContext<CmsContext>();
        _sourceContext = DbFixture.CreateContext<TemplatingTestContext>();
        _service = new PagePublicService(
            _context,
            new PageTemplateResolver(_context),
            new TemplatingService(_context.BuildTemplateSourceResolvers())
        );
    }

    private PagePublicService BuildServiceWithSources() =>
        new(
            _context,
            new PageTemplateResolver(_context),
            new TemplatingService([
                new TemplateSourceResolver<TemplatingTestContext, TemplatingTestSource>(
                    _sourceContext,
                    new TemplateSourceDescriptor(
                        typeof(TemplatingTestSource),
                        nameof(TemplatingTestSource.HostId)))
            ])
        );

    private void SeedSource(Guid hostId, string title)
    {
        _sourceContext.Sources.Add(new TemplatingTestSource { Title = title, HostId = hostId });
        _sourceContext.SaveChanges();
    }

    private static PageBuildPayload Build(string path) => new() { Path = path };

    [Test]
    public async Task BuildPage_ShouldReturnPublishedPage()
    {
        var path = "/static-" + Guid.NewGuid().ToString("N")[..8];
        _context.BuildTestPage(path, true);

        var result = await _service.BuildPublicPage(Build(path));

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(result.Value).IsNotNull();
    }

    [Test]
    public async Task BuildPage_ShouldReturnInvalidPagePath_WhenItsDateHasNotCome()
    {
        var path = "/later-" + Guid.NewGuid().ToString("N")[..8];
        _context.BuildTestPage(path, true, publishedAt: DateTime.UtcNow.AddDays(1));

        var result = await _service.BuildPublicPage(Build(path));

        await Assert.That(result.HasErrorOfType<InvalidPagePathException>()).IsTrue();
    }

    [Test]
    public async Task BuildPage_ShouldReturnPage_WhenItsDateHasCome()
    {
        var path = "/due-" + Guid.NewGuid().ToString("N")[..8];
        _context.BuildTestPage(path, true, publishedAt: DateTime.UtcNow.AddDays(-1));

        var result = await _service.BuildPublicPage(Build(path));

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(result.Value).IsNotNull();
    }

    [Test]
    public async Task BuildPage_ShouldReturnInvalidPagePath_WhenPageIsNotPublished()
    {
        var path = "/unpub-" + Guid.NewGuid().ToString("N")[..8];
        _context.BuildTestPage(path);

        var result = await _service.BuildPublicPage(Build(path));

        await Assert.That(result.HasErrorOfType<InvalidPagePathException>()).IsTrue();
    }

    [Test]
    public async Task BuildPage_ShouldReturnInvalidPagePath_WhenPageDoesNotExist()
    {
        var result = await _service.BuildPublicPage(Build("/missing-" + Guid.NewGuid().ToString("N")[..8]));

        await Assert.That(result.HasErrorOfType<InvalidPagePathException>()).IsTrue();
    }

    [Test]
    public async Task BuildPage_ShouldReturnInvalidPagePath_WhenPathIsEmpty()
    {
        var result = await _service.BuildPublicPage(Build(string.Empty));

        await Assert.That(result.HasErrorOfType<InvalidPagePathException>()).IsTrue();
    }

    [Test]
    public async Task BuildPage_NotTemplated_DoesNotAlterValues()
    {
        var path = "/raw-" + Guid.NewGuid().ToString("N")[..8];
        _context.Pages.Add(new Page
        {
            Path = path,
            Published = true,
            Indexed = true,
            Title = "Plain {{ article.title }} text",
            Description = "No hydration"
        });
        _context.SaveChanges();

        var result = await _service.BuildPublicPage(Build(path));

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(result.Value!.Title).IsEqualTo("Plain {{ article.title }} text");
        await Assert.That(result.Value!.Description).IsEqualTo("No hydration");
    }

    private void SeedOpenGraphEntries(Guid pageId, params (string Property, string Content)[] entries)
    {
        var index = 0;
        foreach (var (property, content) in entries)
        {
            var entry = new OpenGraphEntry { Property = property, Content = content };
            _context.OpenGraphEntries.Add(entry);
            _context.SaveChanges();
            _context.PageOpenGraphs.Add(new PageOpenGraph
            {
                ParentId = pageId,
                ChildId = entry.Id,
                Order = index++
            });
            _context.SaveChanges();
        }
    }

    [Test]
    public async Task BuildPage_IncludesEmptyOpenGraph_WhenNoneAttached()
    {
        var path = "/og-empty-" + Guid.NewGuid().ToString("N")[..8];
        _context.BuildTestPage(path, true);

        var result = await _service.BuildPublicPage(Build(path));

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(result.Value!.OpenGraph).IsEmpty();
    }

    [Test]
    public async Task BuildPage_DropsAnOpenGraphEntry_LeftEmptyByItsSource()
    {
        var path = "/og-blank-" + Guid.NewGuid().ToString("N")[..8];
        var page = _context.BuildTestPage(path, true);
        SeedOpenGraphEntries(page.Id, ("og:title", "Rempli"), ("og:image", "   "));

        var result = await _service.BuildPublicPage(Build(path));

        // What makes an interpolated entry optional: the template offers it to every page it covers,
        // and a page whose source has nothing to put in it does not carry the tag at all.
        await Assert.That(result.Value!.OpenGraph.Select(e => e.Property)).IsEquivalentTo(new[] { "og:title" });
    }

    [Test]
    public async Task BuildPage_IncludesOpenGraphEntries_OrderedByPivot()
    {
        var path = "/og-list-" + Guid.NewGuid().ToString("N")[..8];
        var page = _context.BuildTestPage(path, true);
        SeedOpenGraphEntries(page.Id,
            ("og:title", "First title"),
            ("og:description", "Second desc")
        );

        var result = await _service.BuildPublicPage(Build(path));

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(result.Value!.OpenGraph.Count).IsEqualTo(2);
        await Assert.That(result.Value!.OpenGraph[0].Property).IsEqualTo("og:title");
        await Assert.That(result.Value!.OpenGraph[0].Content).IsEqualTo("First title");
        await Assert.That(result.Value!.OpenGraph[1].Property).IsEqualTo("og:description");
        await Assert.That(result.Value!.OpenGraph[1].Content).IsEqualTo("Second desc");
    }

    [Test]
    public async Task BuildPageSheets_ReturnsInheritedThenOwn_WithoutDuplicates()
    {
        var (template, dedicated) = SeedTemplateAndDedicatedPage();
        var inherited = _context.BuildTestSheet(type: "css", content: "/* inherited */", published: true);
        var own = _context.BuildTestSheet(type: "js", content: "/* own */", published: true);
        _context.BuildTestPageSheet(template.Id, inherited.Id);
        _context.BuildTestPageSheet(dedicated.Id, own.Id);
        // The same sheet hangs off both pages: it must surface once.
        _context.BuildTestPageSheet(dedicated.Id, inherited.Id, loadOrder: 1);

        var result = await _service.BuildPublicPageSheets(Build(dedicated.Path));

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(result.Value!.Count).IsEqualTo(2);
        await Assert.That(result.Value!.Count(s => s.Id == inherited.Id)).IsEqualTo(1);
        await Assert.That(result.Value!.Select(s => s.Id)).IsEquivalentTo(new[] { own.Id, inherited.Id });
    }

    [Test]
    public async Task BuildPageSheets_ExcludesUnpublishedSheets()
    {
        var (page, published) = SeedPageWithSheet(sheetContent: "/* published */");
        var draft = _context.BuildTestSheet(type: "css", content: "/* draft */", published: false);
        _context.BuildTestPageSheet(page.Id, draft.Id, loadOrder: 1);

        var result = await _service.BuildPublicPageSheets(Build(page.Path));

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(result.Value!.Select(s => s.Id)).IsEquivalentTo(new[] { published.Id });
    }

    [Test]
    public async Task BuildPageSheets_ReturnsInvalidPagePath_WhenPathIsUnknown()
    {
        var result = await _service.BuildPublicPageSheets(Build("/nope-" + Guid.NewGuid().ToString("N")[..8]));

        await Assert.That(result.HasErrorOfType<InvalidPagePathException>()).IsTrue();
    }

    private (Page page, Sheet sheet) SeedPageWithSheet(
        string sheetType = "css",
        string sheetContent = "body { color: red; }",
        bool sheetPublished = true,
        bool pagePublished = true,
        bool dynamicPath = false,
        string? path = null
    )
    {
        var page = new Page
        {
            Path = path ?? $"/sheet-{TestId}-{Guid.NewGuid().ToString("N")[..6]}" +
                (dynamicPath ? "/:slug" : string.Empty),
            Published = pagePublished,
            Indexed = true
        };
        var sheet = new Sheet
        {
            Name = "test-sheet-" + Guid.NewGuid().ToString("N")[..6],
            Type = sheetType,
            Content = sheetContent,
            Published = sheetPublished
        };
        _context.Pages.Add(page);
        _context.Sheets.Add(sheet);
        _context.SaveChanges();
        _context.PageSheets.Add(new PageSheet { ParentId = page.Id, ChildId = sheet.Id, Order = 0 });
        _context.SaveChanges();
        return (page, sheet);
    }

    private (Page template, Page dedicated) SeedTemplateAndDedicatedPage(
        string? templateTitle = null,
        string? templateDescription = null,
        string? dedicatedTitle = null,
        string? dedicatedDescription = null,
        bool templatePublished = true
    )
    {
        var prefix = $"/inh-{TestId}-{Guid.NewGuid().ToString("N")[..6]}";
        var template = new Page
        {
            Path = $"{prefix}/:slug",
            Published = templatePublished,
            Indexed = true,
            Title = templateTitle,
            Description = templateDescription
        };
        var dedicated = new Page
        {
            Path = $"{prefix}/child",
            Published = true,
            Indexed = true,
            Title = dedicatedTitle,
            Description = dedicatedDescription
        };
        _context.Pages.AddRange(template, dedicated);
        _context.SaveChanges();
        return (template, dedicated);
    }

    [Test]
    public async Task BuildPage_InheritsTemplateValues_WhenDedicatedFieldIsEmpty()
    {
        var (_, dedicated) = SeedTemplateAndDedicatedPage(
            templateTitle: "Template title",
            templateDescription: "Template desc",
            dedicatedDescription: "Own desc"
        );

        var result = await _service.BuildPublicPage(Build(dedicated.Path));

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(result.Value!.Title).IsEqualTo("Template title");
        await Assert.That(result.Value!.Description).IsEqualTo("Own desc");
    }

    [Test]
    public async Task BuildPage_PrefersFewestDynamicSlugs_WhenSeveralTemplatesMatch()
    {
        var prefix = $"/spec-{TestId}-{Guid.NewGuid().ToString("N")[..6]}";
        _context.Pages.Add(new Page { Path = $"{prefix}/:a/:b", Published = true, Title = "broad" });
        _context.Pages.Add(new Page { Path = $"{prefix}/foo/:b", Published = true, Title = "narrow" });
        // Templates are inherited from, never served: specificity is arbitrated for a real page.
        _context.Pages.Add(new Page { Path = $"{prefix}/foo/bar", Published = true });
        _context.SaveChanges();

        var result = await _service.BuildPublicPage(Build($"{prefix}/foo/bar"));

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(result.Value!.Title).IsEqualTo("narrow");
    }

    [Test]
    public async Task BuildPage_PrefersLongestPath_WhenSpecificityTies()
    {
        var prefix = $"/tie-{TestId}-{Guid.NewGuid().ToString("N")[..6]}";
        _context.Pages.Add(new Page { Path = $"{prefix}/:a/x", Published = true, Title = "short" });
        _context.Pages.Add(new Page { Path = $"{prefix}/foo/:b", Published = true, Title = "long" });
        _context.Pages.Add(new Page { Path = $"{prefix}/foo/x", Published = true });
        _context.SaveChanges();

        var result = await _service.BuildPublicPage(Build($"{prefix}/foo/x"));

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(result.Value!.Title).IsEqualTo("long");
    }

    /// <summary>
    ///     A template covering the path is not a page: without a published page at that exact path the
    ///     build must fail. Serving the template instead would answer 200 for every unknown slug — and
    ///     for a page that was deliberately unpublished — with its raw tokens as title and description,
    ///     since no source hosts on a pattern.
    /// </summary>
    [Test]
    public async Task BuildPage_Fails_WhenOnlyATemplateCoversThePath()
    {
        var prefix = $"/ghost-{TestId}-{Guid.NewGuid().ToString("N")[..6]}";
        _context.Pages.Add(
            new Page { Path = $"{prefix}/:slug", Published = true, Title = "{{ article.title }}" }
        );
        _context.Pages.Add(new Page { Path = $"{prefix}/unpublished", Published = false });
        _context.SaveChanges();

        var missing = await _service.BuildPublicPage(Build($"{prefix}/never-existed"));
        var unpublished = await _service.BuildPublicPage(Build($"{prefix}/unpublished"));

        await Assert.That(missing.HasError).IsTrue();
        await Assert.That(unpublished.HasError).IsTrue();
    }

    [Test]
    public async Task BuildPage_IgnoresUnpublishedTemplate()
    {
        var (_, dedicated) = SeedTemplateAndDedicatedPage(
            templateTitle: "Template title",
            templatePublished: false
        );

        var result = await _service.BuildPublicPage(Build(dedicated.Path));

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(result.Value!.Title).IsNull();
    }

    [Test]
    public async Task BuildPage_ReturnsInvalidPagePath_WhenOnlyUnpublishedTemplateMatches()
    {
        var prefix = $"/unp-tpl-{TestId}-{Guid.NewGuid().ToString("N")[..6]}";
        _context.Pages.Add(new Page { Path = $"{prefix}/:slug", Published = false });
        _context.SaveChanges();

        var result = await _service.BuildPublicPage(Build($"{prefix}/anything"));

        await Assert.That(result.HasErrorOfType<InvalidPagePathException>()).IsTrue();
    }

    [Test]
    public async Task BuildPage_MergesOpenGraph_PerProperty()
    {
        var (template, dedicated) = SeedTemplateAndDedicatedPage();
        SeedOpenGraphEntries(template.Id,
            ("og:title", "Template title"),
            ("og:image", "img-1"),
            ("og:image", "img-2")
        );
        SeedOpenGraphEntries(dedicated.Id, ("og:title", "Own title"));

        var result = await _service.BuildPublicPage(Build(dedicated.Path));

        await Assert.That(result.HasError).IsFalse();
        var og = result.Value!.OpenGraph;
        await Assert.That(og.Count).IsEqualTo(3);
        await Assert.That(og[0].Property).IsEqualTo("og:image");
        await Assert.That(og[0].Content).IsEqualTo("img-1");
        await Assert.That(og[1].Property).IsEqualTo("og:image");
        await Assert.That(og[1].Content).IsEqualTo("img-2");
        await Assert.That(og[2].Property).IsEqualTo("og:title");
        await Assert.That(og[2].Content).IsEqualTo("Own title");
    }
    
    [Test]
    public async Task BuildPage_InterpolatesFromSourceHostedOnTheDedicatedPage()
    {
        var (_, dedicated) = SeedTemplateAndDedicatedPage(
            dedicatedTitle: "{{ templatingtestsource.title }}");
        SeedSource(dedicated.Id, "Own source");

        var result = await BuildServiceWithSources().BuildPublicPage(Build(dedicated.Path));

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(result.Value!.Title).IsEqualTo("Own source");
    }

    /// <summary>
    ///     A template can carry the source of every page it covers: the dedicated page has none of its
    ///     own, so the token is fed by the source hosted on the template it inherits from.
    /// </summary>
    [Test]
    public async Task BuildPage_FallsBackToSourceHostedOnTheTemplate()
    {
        var (template, dedicated) = SeedTemplateAndDedicatedPage(
            dedicatedTitle: "{{ templatingtestsource.title }}");
        SeedSource(template.Id, "Template source");

        var result = await BuildServiceWithSources().BuildPublicPage(Build(dedicated.Path));

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(result.Value!.Title).IsEqualTo("Template source");
    }

    /// <summary>
    ///     Both hosts carry a source: precedence must be the dedicated page, deterministically. Resolving
    ///     both ids in a single "IN" query left the winner to whatever row Postgres returned first.
    /// </summary>
    [Test]
    public async Task BuildPage_PrefersTheDedicatedPageSource_OverTheTemplateOne()
    {
        var (template, dedicated) = SeedTemplateAndDedicatedPage(
            dedicatedTitle: "{{ templatingtestsource.title }}");
        SeedSource(template.Id, "Template source");
        SeedSource(dedicated.Id, "Own source");

        var result = await BuildServiceWithSources().BuildPublicPage(Build(dedicated.Path));

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(result.Value!.Title).IsEqualTo("Own source");
    }

    [Test]
    public async Task BuildPage_LeavesTokensVerbatim_WhenNoSourceIsHosted()
    {
        var (_, dedicated) = SeedTemplateAndDedicatedPage(
            dedicatedTitle: "{{ templatingtestsource.title }}");

        var result = await BuildServiceWithSources().BuildPublicPage(Build(dedicated.Path));

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(result.Value!.Title).IsEqualTo("{{ templatingtestsource.title }}");
    }
}
