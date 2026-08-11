using System.Text.Json;
using Digital.Net.Cms.Context;
using Digital.Net.Cms.Http.Dto;
using Digital.Net.Cms.Http.Services;
using Digital.Net.Cms.Models;
using Digital.Net.Cms.Models.Pages;
using Digital.Net.Lib.Entities.Exceptions;
using Digital.Net.Lib.Entities.Pivots;
using Digital.Net.Core.Http.Services.Crud;
using Digital.Net.Lib.Exceptions.types;
using Digital.Net.Tests.Core;
using Digital.Net.Tests.Core.Factories;
using Digital.Net.Tests.Core.Factories.Data;
using Microsoft.EntityFrameworkCore;
using TUnit.Core.Interfaces;

namespace Digital.Net.Tests.Cms.Http.Services.Pages;

public class PageCrudServiceTest : UnitTest, IAsyncInitializer
{
    [ClassDataSource<DatabaseFixture>]
    public required DatabaseFixture DbFixture { get; init; }

    private CmsContext _context = null!;
    private PageCrudService _service = null!;

    public async Task InitializeAsync()
    {
        await DbFixture.EnsureCreatedAsync<CmsContext>();
        _context = DbFixture.CreateContext<CmsContext>();

        var openGraphResolver = new PivotPatchResolver<
            CmsContext, Page, OpenGraphEntry, PageOpenGraph, OpenGraphEntryPayloadDto
        >(_context);

        var dispatcher = new PatchDispatcher<Page>([openGraphResolver]);
        var crudService = new CrudService<CmsContext, Page>(_context, dispatcher);
        _service = new PageCrudService(_context, crudService);
    }

    /// <summary>
    ///     A page is what holds content together, so deleting it takes the sheets it owns. Only the pivot
    ///     row cascades at the database level, which would otherwise leave sheets nothing renders.
    /// </summary>
    [Test]
    public async Task DeletePage_TakesTheSheetsItOwns()
    {
        var page = new Page { Path = $"/owned-{TestId}-{Guid.NewGuid():N}"[..40], Published = true };
        var sheet = new Sheet { Name = $"own-{TestId}", Type = "html", Content = "<p>x</p>", Published = true };
        _context.Pages.Add(page);
        _context.Sheets.Add(sheet);
        await _context.SaveChangesAsync();
        _context.PageSheets.Add(new PageSheet { ParentId = page.Id, ChildId = sheet.Id, Order = 0 });
        await _context.SaveChangesAsync();

        var result = await _service.DeletePage(page.Id);

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(await _context.Pages.AnyAsync(p => p.Id == page.Id)).IsFalse();
        await Assert.That(await _context.Sheets.AnyAsync(s => s.Id == sheet.Id)).IsFalse();
    }

    /// <summary>
    ///     Inheritance is why the cascade stops at shared sheets: one hung on a template is rendered by
    ///     every page under it, so deleting one of those pages must not empty the others.
    /// </summary>
    [Test]
    public async Task DeletePage_SparesASheetStillAttachedElsewhere()
    {
        var doomed = new Page { Path = $"/gone-{TestId}-{Guid.NewGuid():N}"[..40], Published = true };
        var keeper = new Page { Path = $"/kept-{TestId}-{Guid.NewGuid():N}"[..40], Published = true };
        var shared = new Sheet { Name = $"shared-{TestId}", Type = "css", Content = "body{}", Published = true };
        _context.Pages.AddRange(doomed, keeper);
        _context.Sheets.Add(shared);
        await _context.SaveChangesAsync();
        _context.PageSheets.AddRange(
            new PageSheet { ParentId = doomed.Id, ChildId = shared.Id, Order = 0 },
            new PageSheet { ParentId = keeper.Id, ChildId = shared.Id, Order = 0 }
        );
        await _context.SaveChangesAsync();

        var result = await _service.DeletePage(doomed.Id);

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(await _context.Sheets.AnyAsync(s => s.Id == shared.Id)).IsTrue();
        await Assert.That(
            await _context.PageSheets.AnyAsync(ps => ps.ParentId == keeper.Id && ps.ChildId == shared.Id)
        ).IsTrue();
    }

    private static JsonElement BuildPatch(params object[] ops) => JsonSerializer.SerializeToElement(ops);

    [Test]
    public async Task CreatePage_ShouldRejectInvalidPath()
    {
        var result = await _service.CreatePage(new PagePayload { Path = "/invalid!path" }, Guid.NewGuid());
        await Assert.That(result.HasError).IsTrue();
        await Assert.That(result.HasErrorOfType<EntityValidationException>()).IsTrue();
    }

    [Test]
    public async Task CreatePage_ShouldRejectTrailingSlash()
    {
        var result = await _service.CreatePage(new PagePayload { Path = "/home/" }, Guid.NewGuid());
        await Assert.That(result.HasErrorOfType<EntityValidationException>()).IsTrue();
    }

    [Test]
    public async Task CreatePage_ShouldAcceptDynamicSlugPath()
    {
        var path = "/articles-" + Guid.NewGuid().ToString("N")[..8] + "/:id";
        var result = await _service.CreatePage(new PagePayload { Path = path }, Guid.NewGuid());
        await Assert.That(result.HasError).IsFalse();
        await Assert.That(result.Value).IsNotDefault();
    }

    [Test]
    public async Task CreatePage_ShouldAcceptStaticPath()
    {
        // A page no longer declares which entity feeds it, so a static path needs no dynamic slug.
        var path = "/static-" + Guid.NewGuid().ToString("N")[..8];
        var result = await _service.CreatePage(new PagePayload { Path = path }, Guid.NewGuid());

        await Assert.That(result.HasError).IsFalse();
        var saved = await _context.Pages.AsNoTracking().FirstAsync(p => p.Id == result.Value);
        await Assert.That(saved.Path).IsEqualTo(path);
    }

    [Test]
    public async Task CreatePage_ShouldRejectDuplicatePath()
    {
        var path = "/dup-" + Guid.NewGuid().ToString("N")[..8];
        await _service.CreatePage(new PagePayload { Path = path }, Guid.NewGuid());
        var result = await _service.CreatePage(new PagePayload { Path = path }, Guid.NewGuid());
        await Assert.That(result.HasError).IsTrue();
    }

    [Test]
    public async Task CreatePage_ShouldRejectMissingPath()
    {
        var result = await _service.CreatePage(new PagePayload { Path = "" }, Guid.NewGuid());
        await Assert.That(result.HasError).IsTrue();
    }

    [Test]
    public async Task PatchPage_ShouldAcceptOpenGraphEntries()
    {
        var page = _context.BuildTestPage();
        var patch = BuildPatch(new
        {
            op = "replace",
            path = "/openGraph",
            value = new object[]
            {
                new { property = "og:title", content = "Example" },
                new { property = "og:image", content = "https://example.com/a.jpg" },
                new { property = "og:image", content = "https://example.com/b.jpg" }
            }
        });

        var result = await _service.PatchPage(patch, page.Id, Guid.NewGuid());

        await Assert.That(result.HasError).IsFalse();
        var entries = await _context.Set<PageOpenGraph>()
            .AsNoTracking()
            .Include(p => p.Child)
            .Where(p => p.ParentId == page.Id)
            .ToListAsync();
        await Assert.That(entries.Count).IsEqualTo(3);
        await Assert.That(entries.Count(e => e.Child.Property == "og:image")).IsEqualTo(2);
    }

    [Test]
    public async Task PatchPage_ShouldRejectUnknownOpenGraphKey()
    {
        var page = _context.BuildTestPage();
        var patch = BuildPatch(new
        {
            op = "replace",
            path = "/openGraph",
            value = new[] { new { property = "og:nonexistent", content = "x" } }
        });

        var result = await _service.PatchPage(patch, page.Id, Guid.NewGuid());
        await Assert.That(result.HasErrorOfType<EntityValidationException>()).IsTrue();
    }

    [Test]
    public async Task PatchPage_ShouldClearOpenGraphOnEmptyArray()
    {
        var page = _context.BuildTestPage();
        var setPatch = BuildPatch(new
        {
            op = "replace",
            path = "/openGraph",
            value = new[] { new { property = "og:title", content = "x" } }
        });
        await _service.PatchPage(setPatch, page.Id, Guid.NewGuid());

        var clearPatch = BuildPatch(new { op = "replace", path = "/openGraph", value = Array.Empty<object>() });
        var result = await _service.PatchPage(clearPatch, page.Id, Guid.NewGuid());

        await Assert.That(result.HasError).IsFalse();
        var count = await _context.Set<PageOpenGraph>().AsNoTracking().CountAsync(p => p.ParentId == page.Id);
        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task PatchPage_ShouldRejectEmptyOpenGraphContent()
    {
        var page = _context.BuildTestPage();
        var patch = BuildPatch(new
        {
            op = "replace",
            path = "/openGraph",
            value = new[] { new { property = "og:title", content = "" } }
        });

        var result = await _service.PatchPage(patch, page.Id, Guid.NewGuid());
        await Assert.That(result.HasErrorOfType<EntityValidationException>()).IsTrue();
    }

    [Test]
    public async Task PatchPage_ShouldReturnNotFound_WhenPageDoesNotExist()
    {
        var patch = BuildPatch(new { op = "replace", path = "/Title", value = "x" });
        var result = await _service.PatchPage(patch, Guid.NewGuid(), Guid.NewGuid());
        await Assert.That(result.HasErrorOfType<ResourceNotFoundException>()).IsTrue();
    }

    [Test]
    public async Task PatchPage_ShouldUpdateScalarFields()
    {
        var page = _context.BuildTestPage();
        var patch = BuildPatch(
            new { op = "replace", path = "/Title", value = "New title" },
            new { op = "replace", path = "/Description", value = "New desc" }
        );

        var result = await _service.PatchPage(patch, page.Id, Guid.NewGuid());
        await Assert.That(result.HasError).IsFalse();
        var updated = await _context.Pages.AsNoTracking().FirstAsync(p => p.Id == page.Id);
        await Assert.That(updated.Title).IsEqualTo("New title");
        await Assert.That(updated.Description).IsEqualTo("New desc");
    }

    [Test]
    public async Task PatchPage_ShouldRejectInvalidPathMutation()
    {
        var page = _context.BuildTestPage();
        var patch = BuildPatch(new { op = "replace", path = "/Path", value = "/invalid!path" });
        var result = await _service.PatchPage(patch, page.Id, Guid.NewGuid());
        await Assert.That(result.HasErrorOfType<EntityValidationException>()).IsTrue();
    }

    [Test]
    public async Task PatchPage_ShouldUpdateExistingOpenGraphEntry_WhenIdMatches()
    {
        var page = _context.BuildTestPage();
        var seed = BuildPatch(new
        {
            op = "replace",
            path = "/openGraph",
            value = new[] { new { property = "og:title", content = "First" } }
        });
        await _service.PatchPage(seed, page.Id, Guid.NewGuid());

        var existingId = await _context.Set<PageOpenGraph>()
            .AsNoTracking()
            .Where(p => p.ParentId == page.Id)
            .Select(p => p.ChildId)
            .FirstAsync();

        var update = BuildPatch(new
        {
            op = "replace",
            path = "/openGraph",
            value = new[] { new { id = existingId, property = "og:title", content = "Renamed" } }
        });
        var result = await _service.PatchPage(update, page.Id, Guid.NewGuid());

        await Assert.That(result.HasError).IsFalse();
        var entries = await _context.Set<PageOpenGraph>()
            .AsNoTracking()
            .Include(p => p.Child)
            .Where(p => p.ParentId == page.Id)
            .ToListAsync();
        await Assert.That(entries.Count).IsEqualTo(1);
        await Assert.That(entries[0].ChildId).IsEqualTo(existingId);
        await Assert.That(entries[0].Child.Content).IsEqualTo("Renamed");
    }

    [Test]
    public async Task PatchPage_ShouldDeleteRemovedOpenGraphEntry_WhenAbsentFromArray()
    {
        var page = _context.BuildTestPage();
        var seed = BuildPatch(new
        {
            op = "replace",
            path = "/openGraph",
            value = new[]
            {
                new { property = "og:title", content = "Keep" },
                new { property = "og:description", content = "Drop" }
            }
        });
        await _service.PatchPage(seed, page.Id, Guid.NewGuid());

        var keepId = await _context.Set<PageOpenGraph>()
            .AsNoTracking()
            .Include(p => p.Child)
            .Where(p => p.ParentId == page.Id && p.Child.Property == "og:title")
            .Select(p => p.ChildId)
            .FirstAsync();

        var update = BuildPatch(new
        {
            op = "replace",
            path = "/openGraph",
            value = new[] { new { id = keepId, property = "og:title", content = "Keep" } }
        });
        await _service.PatchPage(update, page.Id, Guid.NewGuid());

        var entries = await _context.Set<PageOpenGraph>()
            .AsNoTracking()
            .Include(p => p.Child)
            .Where(p => p.ParentId == page.Id)
            .ToListAsync();
        await Assert.That(entries.Count).IsEqualTo(1);
        await Assert.That(entries[0].Child.Property).IsEqualTo("og:title");
    }

    [Test]
    public async Task PatchPage_ShouldRespectOpenGraphOrder_FromArrayIndex()
    {
        var page = _context.BuildTestPage();
        var patch = BuildPatch(new
        {
            op = "replace",
            path = "/openGraph",
            value = new[]
            {
                new { property = "og:image", content = "C" },
                new { property = "og:title", content = "A" },
                new { property = "og:description", content = "B" }
            }
        });
        await _service.PatchPage(patch, page.Id, Guid.NewGuid());

        var contents = await _context.Set<PageOpenGraph>()
            .AsNoTracking()
            .Include(p => p.Child)
            .Where(p => p.ParentId == page.Id)
            .OrderBy(p => p.Order)
            .Select(p => p.Child.Content)
            .ToListAsync();
        await Assert.That(contents).IsEquivalentTo(new[] { "C", "A", "B" });
    }

    [Test]
    public async Task PatchPage_ShouldAcceptDuplicateOpenGraphProperties()
    {
        var page = _context.BuildTestPage();
        var patch = BuildPatch(new
        {
            op = "replace",
            path = "/openGraph",
            value = new[]
            {
                new { property = "og:title", content = "First" },
                new { property = "og:title", content = "Second" }
            }
        });

        var result = await _service.PatchPage(patch, page.Id, Guid.NewGuid());

        await Assert.That(result.HasError).IsFalse();
        var count = await _context.Set<PageOpenGraph>().AsNoTracking().CountAsync(p => p.ParentId == page.Id);
        await Assert.That(count).IsEqualTo(2);
    }

    [Test]
    public async Task PatchPage_ShouldApplyScalarAndOpenGraph_InSameRequest()
    {
        var page = _context.BuildTestPage();
        var patch = BuildPatch(
            new { op = "replace", path = "/Title", value = "Combined" },
            new
            {
                op = "replace",
                path = "/openGraph",
                value = new[] { new { property = "og:title", content = "X" } }
            }
        );

        var result = await _service.PatchPage(patch, page.Id, Guid.NewGuid());

        await Assert.That(result.HasError).IsFalse();
        var updated = await _context.Pages.AsNoTracking().FirstAsync(p => p.Id == page.Id);
        await Assert.That(updated.Title).IsEqualTo("Combined");
        var ogCount = await _context.Set<PageOpenGraph>().AsNoTracking().CountAsync(p => p.ParentId == page.Id);
        await Assert.That(ogCount).IsEqualTo(1);
    }
}