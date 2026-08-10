using System.Text.Json;
using Digital.Net.Cms.Context;
using Digital.Net.Cms.Http.Dto;
using Digital.Net.Cms.Models;
using Digital.Net.Cms.Models.Articles;
using Digital.Net.Cms.Models.Pages;
using Digital.Net.Lib.Entities.Exceptions;
using Digital.Net.Lib.Entities.Pivots;
using Digital.Net.Core.Http.Services.Crud;
using Digital.Net.Tests.Core;
using Digital.Net.Tests.Core.Factories;
using Digital.Net.Tests.Core.Factories.Data;
using Microsoft.EntityFrameworkCore;
using TUnit.Core.Interfaces;

namespace Digital.Net.Tests.Cms.Http.Services.Articles;

public class ArticleCrudServiceTest : UnitTest, IAsyncInitializer
{
    [ClassDataSource<DatabaseFixture>]
    public required DatabaseFixture DbFixture { get; init; }

    private CmsContext _context = null!;
    private CrudService<CmsContext, Article> _service = null!;

    public async Task InitializeAsync()
    {
        await DbFixture.EnsureCreatedAsync<CmsContext>();
        _context = DbFixture.CreateContext<CmsContext>();

        var tagResolver = new PivotPatchResolver<
            CmsContext, Article, Tag, ArticleTag, TagPayload
        >(_context);
        var dispatcher = new PatchDispatcher<Article>([tagResolver]);
        _service = new CrudService<CmsContext, Article>(_context, dispatcher);
    }

    private static JsonElement BuildPatch(params object[] ops) => JsonSerializer.SerializeToElement(ops);

    [Test]
    public async Task PatchArticle_ShouldUpdatePageId()
    {
        var page = _context.BuildTestPage();
        var article = _context.BuildTestArticle();
        var patch = BuildPatch(new { op = "replace", path = "/PageId", value = page.Id });

        var result = await _service.Patch(patch, article.Id);

        await Assert.That(result.HasError).IsFalse();
        var refreshed = await _context.Articles.AsNoTracking().FirstAsync(a => a.Id == article.Id);
        await Assert.That(refreshed.PageId).IsEqualTo(page.Id);
    }

    [Test]
    public async Task PatchArticle_ShouldFail_WhenPageIdDoesNotExist()
    {
        var article = _context.BuildTestArticle();
        var patch = BuildPatch(new { op = "replace", path = "/PageId", value = Guid.NewGuid() });

        var result = await _service.Patch(patch, article.Id);

        await Assert.That(result.HasErrorOfType<EntityValidationException>()).IsTrue();
    }

    [Test]
    public async Task PatchArticle_ShouldAttachExistingTags()
    {
        var tag1 = _context.BuildTestTag("attach-1-" + Guid.NewGuid().ToString("N")[..6]);
        var tag2 = _context.BuildTestTag("attach-2-" + Guid.NewGuid().ToString("N")[..6]);
        var article = _context.BuildTestArticle();
        var patch = BuildPatch(new
        {
            op = "replace",
            path = "/tags",
            value = new[]
            {
                new { id = tag1.Id, name = tag1.Name, color = tag1.Color },
                new { id = tag2.Id, name = tag2.Name, color = tag2.Color }
            }
        });

        var result = await _service.Patch(patch, article.Id);

        await Assert.That(result.HasError).IsFalse();
        var refreshed = await _context.Articles.AsNoTracking().Include(a => a.Tags).FirstAsync(a => a.Id == article.Id);
        await Assert.That(refreshed.Tags.Count).IsEqualTo(2);
        await Assert.That(refreshed.Tags.Any(t => t.Id == tag1.Id)).IsTrue();
        await Assert.That(refreshed.Tags.Any(t => t.Id == tag2.Id)).IsTrue();
    }

    [Test]
    public async Task PatchArticle_ShouldDetachTag_WhileKeepingTagItself()
    {
        var kept = _context.BuildTestTag("keep-" + Guid.NewGuid().ToString("N")[..6]);
        var detached = _context.BuildTestTag("detach-" + Guid.NewGuid().ToString("N")[..6]);
        var article = _context.BuildTestArticle(tags: [kept, detached]);
        var patch = BuildPatch(new
        {
            op = "replace",
            path = "/tags",
            value = new[] { new { id = kept.Id, name = kept.Name, color = kept.Color } }
        });

        var result = await _service.Patch(patch, article.Id);

        await Assert.That(result.HasError).IsFalse();
        var refreshed = await _context.Articles.AsNoTracking().Include(a => a.Tags).FirstAsync(a => a.Id == article.Id);
        await Assert.That(refreshed.Tags.Count).IsEqualTo(1);
        await Assert.That(refreshed.Tags.Single().Id).IsEqualTo(kept.Id);
        await Assert.That(_context.Tags.AsNoTracking().Any(t => t.Id == detached.Id)).IsTrue();
    }

    [Test]
    public async Task PatchArticle_ShouldClearAllTags_WhenEmptyArray()
    {
        var tag = _context.BuildTestTag("clear-" + Guid.NewGuid().ToString("N")[..6]);
        var article = _context.BuildTestArticle(tags: [tag]);
        var patch = BuildPatch(new { op = "replace", path = "/tags", value = Array.Empty<object>() });

        var result = await _service.Patch(patch, article.Id);

        await Assert.That(result.HasError).IsFalse();
        var refreshed = await _context.Articles.AsNoTracking().Include(a => a.Tags).FirstAsync(a => a.Id == article.Id);
        await Assert.That(refreshed.Tags.Count).IsEqualTo(0);
        await Assert.That(_context.Tags.AsNoTracking().Any(t => t.Id == tag.Id)).IsTrue();
    }

    [Test]
    public async Task PatchArticle_ShouldOrchestratePageIdAndTags_InSameTransaction()
    {
        var page = _context.BuildTestPage();
        var tag = _context.BuildTestTag("mix-" + Guid.NewGuid().ToString("N")[..6]);
        var article = _context.BuildTestArticle();
        var patch = BuildPatch(
            new { op = "replace", path = "/PageId", value = page.Id },
            new
            {
                op = "replace",
                path = "/tags",
                value = new[] { new { id = tag.Id, name = tag.Name, color = tag.Color } }
            }
        );

        var result = await _service.Patch(patch, article.Id);

        await Assert.That(result.HasError).IsFalse();
        var refreshed = await _context.Articles.AsNoTracking().Include(a => a.Tags).FirstAsync(a => a.Id == article.Id);
        await Assert.That(refreshed.PageId).IsEqualTo(page.Id);
        await Assert.That(refreshed.Tags.Count).IsEqualTo(1);
        await Assert.That(refreshed.Tags.Single().Id).IsEqualTo(tag.Id);
    }

    [Test]
    public async Task PatchArticle_ShouldCreateAndAttachTag_WhenIdMissing()
    {
        var article = _context.BuildTestArticle();
        var newName = "inline-" + Guid.NewGuid().ToString("N")[..6];
        var patch = BuildPatch(new
        {
            op = "replace",
            path = "/tags",
            value = new[] { new { name = newName, color = "#ff00aa" } }
        });

        var result = await _service.Patch(patch, article.Id);

        await Assert.That(result.HasError).IsFalse();
        var refreshed = await _context.Articles.AsNoTracking().Include(a => a.Tags).FirstAsync(a => a.Id == article.Id);
        await Assert.That(refreshed.Tags.Count).IsEqualTo(1);
        var created = refreshed.Tags.Single();
        await Assert.That(created.Name).IsEqualTo(newName);
        await Assert.That(created.Color).IsEqualTo("#ff00aa");
        await Assert.That(_context.Tags.AsNoTracking().Any(t => t.Id == created.Id)).IsTrue();
    }

    [Test]
    public async Task PatchArticle_ShouldFail_WhenAttachingNonExistingTagId()
    {
        var article = _context.BuildTestArticle();
        var patch = BuildPatch(new
        {
            op = "replace",
            path = "/tags",
            value = new[] { new { id = Guid.NewGuid(), name = "ghost", color = (string?)null } }
        });

        var result = await _service.Patch(patch, article.Id);

        await Assert.That(result.HasErrorOfType<EntityValidationException>()).IsTrue();
    }

    [Test]
    public async Task PatchArticle_ShouldFail_WhenTagPayloadIsInvalid()
    {
        var article = _context.BuildTestArticle();
        var patch = BuildPatch(new
        {
            op = "replace",
            path = "/tags",
            value = new[] { new { name = "" } }
        });

        var result = await _service.Patch(patch, article.Id);

        await Assert.That(result.HasErrorOfType<EntityValidationException>()).IsTrue();
    }

    [Test]
    public async Task DeletePage_ShouldSetArticlePageIdToNull()
    {
        var page = _context.BuildTestPage();
        var article = _context.BuildTestArticle(pageId: page.Id);

        _context.Pages.Remove(page);
        await _context.SaveChangesAsync();

        var refreshed = await _context.Articles.AsNoTracking().FirstAsync(a => a.Id == article.Id);
        await Assert.That(refreshed.PageId).IsNull();
    }

    [Test]
    public async Task DeleteTag_ShouldRemoveArticleTagAssociations_ButKeepArticles()
    {
        var shared = _context.BuildTestTag("shared-" + Guid.NewGuid().ToString("N")[..6]);
        var articleA = _context.BuildTestArticle(tags: [shared]);
        var articleB = _context.BuildTestArticle(tags: [shared]);

        _context.Tags.Remove(shared);
        await _context.SaveChangesAsync();

        var refreshedA = await _context.Articles.AsNoTracking().Include(a => a.Tags).FirstAsync(a => a.Id == articleA.Id);
        var refreshedB = await _context.Articles.AsNoTracking().Include(a => a.Tags).FirstAsync(a => a.Id == articleB.Id);
        await Assert.That(refreshedA.Tags.Any(t => t.Id == shared.Id)).IsFalse();
        await Assert.That(refreshedB.Tags.Any(t => t.Id == shared.Id)).IsFalse();
        await Assert.That(_context.Tags.AsNoTracking().Any(t => t.Id == shared.Id)).IsFalse();
    }
}
