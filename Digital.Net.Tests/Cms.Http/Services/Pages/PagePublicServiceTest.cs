using Digital.Net.Cms.Context;
using Digital.Net.Cms.Http.Dto;
using Digital.Net.Cms.Http.Exceptions;
using Digital.Net.Cms.Http.Services;
using Digital.Net.Cms.Models;
using Digital.Net.Cms.Models.Articles;
using Digital.Net.Cms.Models.Pages;
using Digital.Net.Tests.Core;
using Digital.Net.Tests.Core.Factories;
using Digital.Net.Tests.Core.Factories.Data;
using TUnit.Core.Interfaces;

namespace Digital.Net.Tests.Cms.Http.Services.Pages;

public class PagePublicServiceTest : UnitTest, IAsyncInitializer
{
    [ClassDataSource<DatabaseFixture>]
    public required DatabaseFixture DbFixture { get; init; }
    
    private CmsContext _context = null!;
    private PagePublicService _service = null!;

    public async Task InitializeAsync()
    {
        await DbFixture.EnsureCreatedAsync<CmsContext>();
        _context = DbFixture.CreateContext<CmsContext>();
        _service = new PagePublicService(
            _context,
            new PageTemplateResolver(_context),
            _context.BuildTemplateSourceResolvers()
        );
    }

    private static PageBuildPayload Build(string path, string? slug = null) =>
        new() { Path = path, PageSlug = slug };

    private (string pattern, string slug) SeedArticleAndTemplatedPage(
        string articleTitle = "Hello World",
        string articleDescription = "Greetings from prod",
        string? pageTitle = "Blog: {{ article.title }}",
        string? pageDescription = "Read: {{ article.description }}"
    )
    {
        var slug = "tpl-" + Guid.NewGuid().ToString("N")[..8];
        var pattern = $"/blog-{TestId}-{Guid.NewGuid().ToString("N")[..6]}/:slug";

        var page = new Page
        {
            Path = pattern,
            Published = true,
            Indexed = true,
            Title = pageTitle,
            Description = pageDescription
        };
        _context.Pages.Add(page);
        _context.SaveChanges();
        _context.Articles.Add(new Article
        {
            Slug = slug,
            Title = articleTitle,
            Description = articleDescription,
            Content = "body",
            PublishedAt = DateTime.UtcNow,
            PageId = page.Id
        });
        _context.SaveChanges();
        return (pattern, slug);
    }

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
    public async Task BuildPage_HydratesTemplateVariables_FromMatchingArticle()
    {
        var (pattern, slug) = SeedArticleAndTemplatedPage();

        var result = await _service.BuildPublicPage(Build(pattern,slug));

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(result.Value!.Title).IsEqualTo("Blog: Hello World");
        await Assert.That(result.Value!.Description).IsEqualTo("Read: Greetings from prod");
    }

    [Test]
    public async Task BuildPage_LeavesPlaceholdersUnchanged_WhenSourceFieldUnknown()
    {
        var (pattern, slug) = SeedArticleAndTemplatedPage(
            pageTitle: "T",
            pageDescription: "Junk: {{ article.unknown }}"
        );

        var result = await _service.BuildPublicPage(Build(pattern,slug));

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(result.Value!.Description).IsEqualTo("Junk: {{ article.unknown }}");
    }

    [Test]
    public async Task BuildPage_ReturnsInvalidPagePath_WhenTemplatedPage_ButArticleNotFound()
    {
        var pattern = $"/no-article-{TestId}/:slug";
        _context.Pages.Add(new Page
        {
            Path = pattern,
            Published = true,
            Indexed = true,
            Title = "Blog: {{ article.title }}"
        });
        _context.SaveChanges();

        var result = await _service.BuildPublicPage(
            Build(pattern,"no-such-article-" + Guid.NewGuid().ToString("N")[..8]));

        await Assert.That(result.HasErrorOfType<InvalidPagePathException>()).IsTrue();
    }

    [Test]
    public async Task BuildPage_DoesNotHydrate_WhenPayloadMissesSlug()
    {
        // Page templatée + payload sans PageSlug : on retourne la page telle quelle, sans
        // hydrater. Le client a déclaré le bon PageType, mais ne demande pas d'instance —
        // utile pour récupérer le squelette de la page.
        var pattern = $"/no-slug-{TestId}/:slug";
        _context.Pages.Add(new Page
        {
            Path = pattern,
            Published = true,
            Indexed = true,
            Title = "Blog: {{ article.title }}"
        });
        _context.SaveChanges();

        var result = await _service.BuildPublicPage(Build(pattern));

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(result.Value!.Title).IsEqualTo("Blog: {{ article.title }}");
    }

    [Test]
    public async Task BuildPage_ReturnsPage_WhenArticlePageIdMatches()
    {
        var slug = "match-" + Guid.NewGuid().ToString("N")[..8];
        var page = _context.BuildTestPage(
            path: $"/m-{TestId}-{Guid.NewGuid().ToString("N")[..6]}/:slug",
            published: true
        );
        _context.BuildTestArticle(slug: slug, published: true, pageId: page.Id);

        var result = await _service.BuildPublicPage(Build(page.Path,slug));

        await Assert.That(result.HasError).IsFalse();
    }

    [Test]
    public async Task BuildPage_ReturnsInvalidPagePath_WhenArticlePageIdMismatches()
    {
        var slug = "shared-" + Guid.NewGuid().ToString("N")[..8];
        var pageA = _context.BuildTestPage(
            path: $"/a-{TestId}-{Guid.NewGuid().ToString("N")[..6]}/:slug",
            published: true
        );
        var pageB = _context.BuildTestPage(
            path: $"/b-{TestId}-{Guid.NewGuid().ToString("N")[..6]}/:slug",
            published: true
        );
        _context.BuildTestArticle(slug: slug, published: true, pageId: pageA.Id);

        var result = await _service.BuildPublicPage(Build(pageB.Path,slug));

        await Assert.That(result.HasErrorOfType<InvalidPagePathException>()).IsTrue();
    }

    [Test]
    public async Task BuildPage_ReturnsInvalidPagePath_WhenArticleIsOrphan()
    {
        var slug = "orphan-" + Guid.NewGuid().ToString("N")[..8];
        var page = _context.BuildTestPage(
            path: $"/o-{TestId}-{Guid.NewGuid().ToString("N")[..6]}/:slug",
            published: true
        );
        _context.BuildTestArticle(slug: slug, published: true, pageId: null);

        var result = await _service.BuildPublicPage(Build(page.Path,slug));

        await Assert.That(result.HasErrorOfType<InvalidPagePathException>()).IsTrue();
    }

    [Test]
    public async Task BuildPage_ReturnsInvalidPagePath_WhenArticleNotPublished()
    {
        var slug = "draft-" + Guid.NewGuid().ToString("N")[..8];
        var page = _context.BuildTestPage(
            path: $"/d-{TestId}-{Guid.NewGuid().ToString("N")[..6]}/:slug",
            published: true
        );
        _context.BuildTestArticle(slug: slug, published: false, pageId: page.Id);

        var result = await _service.BuildPublicPage(Build(page.Path,slug));

        await Assert.That(result.HasErrorOfType<InvalidPagePathException>()).IsTrue();
    }

    [Test]
    public async Task BuildPage_ServesStaticPage_WhenPayloadProvidesAnUnknownSlug()
    {
        // A static page owns its path: an unresolvable slug does not turn it into a 404.
        var path = "/static-pt-" + Guid.NewGuid().ToString("N")[..8];
        _context.BuildTestPage(path, true);

        var result = await _service.BuildPublicPage(Build(path, "some-slug"));

        await Assert.That(result.HasError).IsFalse();
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
    public async Task BuildPage_HydratesOpenGraphContent_FromMatchingArticle()
    {
        var (pattern, slug) = SeedArticleAndTemplatedPage();
        var page = _context.Pages.Single(p => p.Path == pattern);
        SeedOpenGraphEntries(page.Id,
            ("og:title", "{{ article.title }}"),
            ("og:description", "About {{ article.description }}")
        );

        var result = await _service.BuildPublicPage(Build(pattern,slug));

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(result.Value!.OpenGraph[0].Content).IsEqualTo("Hello World");
        await Assert.That(result.Value!.OpenGraph[1].Content).IsEqualTo("About Greetings from prod");
    }

    [Test]
    public async Task BuildPage_LeavesOpenGraphPlaceholders_WhenPayloadMissesSlug()
    {
        var pattern = $"/og-no-slug-{TestId}/:slug";
        _context.Pages.Add(new Page
        {
            Path = pattern,
            Published = true,
            Indexed = true
        });
        _context.SaveChanges();
        var page = _context.Pages.Single(p => p.Path == pattern);
        SeedOpenGraphEntries(page.Id, ("og:title", "{{ article.title }}"));

        var result = await _service.BuildPublicPage(Build(pattern));

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(result.Value!.OpenGraph[0].Content).IsEqualTo("{{ article.title }}");
    }

    private static PageSheetBuildPayload BuildSheet(Guid sheetId, string path, string? slug = null) =>
        new() { SheetId = sheetId, Path = path, PageSlug = slug };

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

    [Test]
    public async Task BuildPageSheet_HydratesContent_FromMatchingArticle()
    {
        var slug = "art-" + Guid.NewGuid().ToString("N")[..8];
        var (page, sheet) = SeedPageWithSheet(
            "css",
            ".author::before { content: '{{ article.title }}'; }",
            dynamicPath: true
        );
        _context.Articles.Add(new Article
        {
            Slug = slug,
            Title = "Hello World",
            Description = "D",
            Content = "C",
            PublishedAt = DateTime.UtcNow,
            PageId = page.Id
        });
        _context.SaveChanges();

        var result = await _service.BuildPublicPageSheetResource(
            BuildSheet(sheet.Id, page.Path,slug));

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(result.Value.contentType).IsEqualTo("text/css");
        await Assert.That(result.Value.content).IsEqualTo(".author::before { content: 'Hello World'; }");
    }

    [Test]
    [Arguments("css", "text/css")]
    [Arguments("js", "application/javascript")]
    [Arguments("html", "text/html")]
    public async Task BuildPageSheet_ReturnsCorrectContentType(string type, string expectedContentType)
    {
        var (page, sheet) = SeedPageWithSheet(type, "raw");

        var result = await _service.BuildPublicPageSheetResource(BuildSheet(sheet.Id, page.Path));

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(result.Value.contentType).IsEqualTo(expectedContentType);
        await Assert.That(result.Value.content).IsEqualTo("raw");
    }

    [Test]
    public async Task BuildPageSheet_NotTemplated_DoesNotAlterContent()
    {
        var (page, sheet) = SeedPageWithSheet(
            "html",
            "<p>{{ article.title }}</p>"
        );

        var result = await _service.BuildPublicPageSheetResource(BuildSheet(sheet.Id, page.Path));

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(result.Value.content).IsEqualTo("<p>{{ article.title }}</p>");
    }

    [Test]
    public async Task BuildPageSheet_LeavesPlaceholdersUnchanged_WhenPayloadMissesSlug()
    {
        var (page, sheet) = SeedPageWithSheet(
            "html",
            "<p>{{ article.title }}</p>",
            dynamicPath: true
        );

        var result = await _service.BuildPublicPageSheetResource(
            BuildSheet(sheet.Id, page.Path));

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(result.Value.content).IsEqualTo("<p>{{ article.title }}</p>");
    }

    [Test]
    public async Task BuildPageSheet_ReturnsInvalidPagePath_WhenSheetIdUnknown()
    {
        var (page, _) = SeedPageWithSheet();

        var result = await _service.BuildPublicPageSheetResource(BuildSheet(Guid.NewGuid(), page.Path));

        await Assert.That(result.HasErrorOfType<InvalidPagePathException>()).IsTrue();
    }

    [Test]
    public async Task BuildPageSheet_ReturnsInvalidPagePath_WhenSheetNotPublished()
    {
        var (page, sheet) = SeedPageWithSheet(sheetPublished: false);

        var result = await _service.BuildPublicPageSheetResource(BuildSheet(sheet.Id, page.Path));

        await Assert.That(result.HasErrorOfType<InvalidPagePathException>()).IsTrue();
    }

    [Test]
    public async Task BuildPageSheet_ReturnsInvalidPagePath_WhenSheetNotLinkedToPage()
    {
        // Page A + Page B + Sheet liée seulement à A. On demande la sheet sur le path de B.
        var (_, sheetA) = SeedPageWithSheet();
        var pageB = new Page
        {
            Path = $"/other-{TestId}-{Guid.NewGuid().ToString("N")[..6]}",
            Published = true,
            Indexed = true
        };
        _context.Pages.Add(pageB);
        _context.SaveChanges();

        var result = await _service.BuildPublicPageSheetResource(BuildSheet(sheetA.Id, pageB.Path));

        await Assert.That(result.HasErrorOfType<InvalidPagePathException>()).IsTrue();
    }

    [Test]
    public async Task BuildPageSheet_ReturnsInvalidPagePath_WhenPageNotPublished()
    {
        var (page, sheet) = SeedPageWithSheet(pagePublished: false);

        var result = await _service.BuildPublicPageSheetResource(BuildSheet(sheet.Id, page.Path));

        await Assert.That(result.HasErrorOfType<InvalidPagePathException>()).IsTrue();
    }

    [Test]
    public async Task BuildPageSheet_ReturnsInvalidPagePath_WhenPathEmpty()
    {
        var result = await _service.BuildPublicPageSheetResource(BuildSheet(Guid.NewGuid(), string.Empty));

        await Assert.That(result.HasErrorOfType<InvalidPagePathException>()).IsTrue();
    }

    [Test]
    public async Task BuildPageSheet_ReturnsInvalidPagePath_WhenArticleNotFound()
    {
        var (page, sheet) = SeedPageWithSheet(dynamicPath: true);

        var result = await _service.BuildPublicPageSheetResource(
            BuildSheet(sheet.Id, page.Path,"ghost-" + Guid.NewGuid().ToString("N")[..8]));

        await Assert.That(result.HasErrorOfType<InvalidPagePathException>()).IsTrue();
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
        _context.SaveChanges();

        var result = await _service.BuildPublicPage(Build($"{prefix}/foo/x"));

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(result.Value!.Title).IsEqualTo("long");
    }

    [Test]
    public async Task BuildPage_FallsBackToTemplate_WhenNoDedicatedPage()
    {
        var (pattern, slug) = SeedArticleAndTemplatedPage();
        var realPath = pattern.Replace(":slug", slug);

        var result = await _service.BuildPublicPage(Build(realPath,slug));

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(result.Value!.Title).IsEqualTo("Blog: Hello World");
    }

    [Test]
    public async Task BuildPage_ServesDedicatedPage_WhenInterpolationSourceMissing()
    {
        var (_, dedicated) = SeedTemplateAndDedicatedPage(
            templateTitle: "Blog: {{ article.title }}"
        );

        var result = await _service.BuildPublicPage(
            Build(dedicated.Path,"ghost-" + Guid.NewGuid().ToString("N")[..8]));

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(result.Value!.Title).IsEqualTo("Blog: {{ article.title }}");
    }

    [Test]
    public async Task BuildPage_HydratesInheritedValues_FromTemplateArticle()
    {
        var (template, dedicated) = SeedTemplateAndDedicatedPage(
            templateTitle: "Blog: {{ article.title }}"
        );
        var slug = "child-" + Guid.NewGuid().ToString("N")[..8];
        _context.Articles.Add(new Article
        {
            Slug = slug,
            Title = "Hello World",
            Description = "D",
            Content = "C",
            PublishedAt = DateTime.UtcNow,
            PageId = template.Id
        });
        _context.SaveChanges();

        var result = await _service.BuildPublicPage(Build(dedicated.Path,slug));

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(result.Value!.Title).IsEqualTo("Blog: Hello World");
    }

    [Test]
    public async Task BuildPage_ReturnsInvalidPagePath_WhenTemplateOnly_AndSourceMissing()
    {
        var (pattern, _) = SeedArticleAndTemplatedPage();
        var realPath = pattern.Replace(":slug", "ghost-" + Guid.NewGuid().ToString("N")[..8]);

        var result = await _service.BuildPublicPage(
            Build(realPath,"ghost-" + Guid.NewGuid().ToString("N")[..8]));

        await Assert.That(result.HasErrorOfType<InvalidPagePathException>()).IsTrue();
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
    public async Task GetPageSheetInfos_ListsTemplateSheetsFirst()
    {
        var (template, dedicated) = SeedTemplateAndDedicatedPage();
        var inherited = _context.BuildTestSheet(name: "inherited", published: true);
        var own = _context.BuildTestSheet(name: "own", published: true);
        _context.BuildTestPageSheet(template.Id, inherited.Id);
        _context.BuildTestPageSheet(dedicated.Id, own.Id);

        var result = await _service.GetPageSheetInfos(dedicated.Id);

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(result.Value!.Count).IsEqualTo(2);
        await Assert.That(result.Value![0].Name).IsEqualTo("inherited");
        await Assert.That(result.Value![1].Name).IsEqualTo("own");
    }

    [Test]
    public async Task GetPageSheetInfos_ExcludesUnpublishedTemplateSheets()
    {
        var (template, dedicated) = SeedTemplateAndDedicatedPage();
        var unpublished = _context.BuildTestSheet(name: "unpublished", published: false);
        _context.BuildTestPageSheet(template.Id, unpublished.Id);

        var result = await _service.GetPageSheetInfos(dedicated.Id);

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(result.Value!).IsEmpty();
    }

    [Test]
    public async Task GetPageSheetInfos_DedupsSheetsSharedWithTemplate()
    {
        var (template, dedicated) = SeedTemplateAndDedicatedPage();
        var inherited = _context.BuildTestSheet(name: "inherited", published: true);
        var shared = _context.BuildTestSheet(name: "shared", published: true);
        _context.BuildTestPageSheet(template.Id, inherited.Id);
        _context.BuildTestPageSheet(template.Id, shared.Id, 1);
        _context.BuildTestPageSheet(dedicated.Id, shared.Id);

        var result = await _service.GetPageSheetInfos(dedicated.Id);

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(result.Value!.Count).IsEqualTo(2);
        await Assert.That(result.Value![0].Name).IsEqualTo("inherited");
        await Assert.That(result.Value![1].Name).IsEqualTo("shared");
    }

    [Test]
    public async Task GetPageSheetInfos_DoesNotInherit_WhenPageIsTemplate()
    {
        var (template, _) = SeedTemplateAndDedicatedPage();
        var own = _context.BuildTestSheet(name: "own", published: true);
        _context.BuildTestPageSheet(template.Id, own.Id);

        var result = await _service.GetPageSheetInfos(template.Id);

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(result.Value!.Count).IsEqualTo(1);
        await Assert.That(result.Value![0].Name).IsEqualTo("own");
    }

    [Test]
    public async Task BuildPageSheet_ServesTemplateSheet_OnDedicatedPagePath()
    {
        var (template, dedicated) = SeedTemplateAndDedicatedPage();
        var sheet = _context.BuildTestSheet(type: "css", content: "body { color: red; }", published: true);
        _context.BuildTestPageSheet(template.Id, sheet.Id);

        var result = await _service.BuildPublicPageSheetResource(BuildSheet(sheet.Id, dedicated.Path));

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(result.Value.contentType).IsEqualTo("text/css");
        await Assert.That(result.Value.content).IsEqualTo("body { color: red; }");
    }

    [Test]
    public async Task BuildPageSheet_ReturnsInvalidPagePath_WhenTemplateSheetUnpublished()
    {
        var (template, dedicated) = SeedTemplateAndDedicatedPage();
        var sheet = _context.BuildTestSheet(published: false);
        _context.BuildTestPageSheet(template.Id, sheet.Id);

        var result = await _service.BuildPublicPageSheetResource(BuildSheet(sheet.Id, dedicated.Path));

        await Assert.That(result.HasErrorOfType<InvalidPagePathException>()).IsTrue();
    }

    [Test]
    public async Task BuildPageSheet_HydratesTemplateSheet_FromTemplateArticle()
    {
        var (template, dedicated) = SeedTemplateAndDedicatedPage();
        var sheet = _context.BuildTestSheet(
            type: "html",
            content: "<p>{{ article.title }}</p>",
            published: true
        );
        _context.BuildTestPageSheet(template.Id, sheet.Id);
        var slug = "sheet-" + Guid.NewGuid().ToString("N")[..8];
        _context.Articles.Add(new Article
        {
            Slug = slug,
            Title = "Hello World",
            Description = "D",
            Content = "C",
            PublishedAt = DateTime.UtcNow,
            PageId = template.Id
        });
        _context.SaveChanges();

        var result = await _service.BuildPublicPageSheetResource(
            BuildSheet(sheet.Id, dedicated.Path,slug));

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(result.Value.content).IsEqualTo("<p>Hello World</p>");
    }
}
