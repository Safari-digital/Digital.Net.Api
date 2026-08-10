using System.Net;
using System.Net.Http.Json;
using Digital.Net.Cms.Http.Dto;
using Digital.Net.Cms.Models.Pages;
using Digital.Net.Core.Http.Services.Pagination;
using Digital.Net.Lib.Messages;
using Digital.Net.Tests.Core.Factories;
using Digital.Net.Tests.Core.Factories.Data;
using Digital.Net.Tests.Core.Factories.Data.Records;
using Digital.Net.Tests.Core.Sdk;

namespace Digital.Net.Tests.Cms.Http.Endpoints;

public class ArticleEndpointsTest
{
    [ClassDataSource<ApplicationFixture>]
    public required ApplicationFixture ApplicationFixture { get; init; }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var user = ApplicationFixture.CreateUser(new TestUserPayload { IsActive = true });
        var client = ApplicationFixture.CreateClient();
        await client.Login(user);
        return client;
    }

    [Test]
    public async Task GetArticles_ShouldPopulateTags_ViaInclude()
    {
        var client = await CreateAuthenticatedClientAsync();
        var ctx = ApplicationFixture.GetCmsContext();
        var tag1 = ctx.BuildTestTag("include-tag-1-" + Guid.NewGuid().ToString("N")[..6]);
        var tag2 = ctx.BuildTestTag("include-tag-2-" + Guid.NewGuid().ToString("N")[..6]);
        var article = ctx.BuildTestArticle(tags: [tag1, tag2]);

        var response = await client.GetArticles(new ArticleQuery { Size = 50, Index = 1 });
        var result = await response.Content.ReadFromJsonAsync<QueryResult<ArticleListDto>>();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = result!.Value.First(a => a.Id == article.Id);
        await Assert.That(dto.Tags.Count).IsEqualTo(2);
        await Assert.That(dto.Tags.Any(t => t.Id == tag1.Id)).IsTrue();
        await Assert.That(dto.Tags.Any(t => t.Id == tag2.Id)).IsTrue();
    }

    [Test]
    public async Task GetArticles_ShouldFilterByTagId_ThroughPivotSkipNav()
    {
        var client = await CreateAuthenticatedClientAsync();
        var ctx = ApplicationFixture.GetCmsContext();
        var targetTag = ctx.BuildTestTag("filter-tag-" + Guid.NewGuid().ToString("N")[..6]);
        var taggedArticle = ctx.BuildTestArticle(tags: [targetTag]);
        ctx.BuildTestArticle();

        var response = await client.GetArticles(new ArticleQuery { TagId = targetTag.Id, Size = 50, Index = 1 });
        var result = await response.Content.ReadFromJsonAsync<QueryResult<ArticleListDto>>();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(result!.Value.All(a => a.Tags.Any(t => t.Id == targetTag.Id))).IsTrue();
        await Assert.That(result.Value.Any(a => a.Id == taggedArticle.Id)).IsTrue();
    }

    [Test]
    public async Task GetArticles_ShouldFilterByPageId()
    {
        var client = await CreateAuthenticatedClientAsync();
        var ctx = ApplicationFixture.GetCmsContext();
        var pageA = ctx.BuildTestPage();
        var pageB = ctx.BuildTestPage();
        var matched = ctx.BuildTestArticle(pageId: pageA.Id);
        ctx.BuildTestArticle(pageId: pageB.Id);

        var response = await client.GetArticles(new ArticleQuery { PageId = pageA.Id, Size = 50, Index = 1 });
        var result = await response.Content.ReadFromJsonAsync<QueryResult<ArticleListDto>>();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(result!.Value.Any(a => a.Id == matched.Id)).IsTrue();
        await Assert.That(result.Value.All(a => a.PageId == pageA.Id)).IsTrue();
    }

    [Test]
    public async Task GetArticleById_ShouldExposePageId()
    {
        var client = await CreateAuthenticatedClientAsync();
        var ctx = ApplicationFixture.GetCmsContext();
        var page = ctx.BuildTestPage();
        var article = ctx.BuildTestArticle(pageId: page.Id);

        var response = await client.GetArticleById(article.Id);
        var result = await response.Content.ReadFromJsonAsync<Result<ArticleDto>>();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(result!.Value!.PageId).IsEqualTo(page.Id);
    }

    [Test]
    public async Task GetSlugAvailability_ShouldReturnTrue_WhenSlugIsFree()
    {
        var client = await CreateAuthenticatedClientAsync();
        var freeSlug = "free-" + Guid.NewGuid().ToString("N")[..8];

        var response = await client.GetSlugAvailability(freeSlug);
        var result = await response.Content.ReadFromJsonAsync<Result<bool>>();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(result!.Value).IsTrue();
    }

    [Test]
    public async Task GetSlugAvailability_ShouldReturnFalse_WhenSlugIsTaken()
    {
        var client = await CreateAuthenticatedClientAsync();
        var ctx = ApplicationFixture.GetCmsContext();
        var takenSlug = "taken-" + Guid.NewGuid().ToString("N")[..8];
        ctx.BuildTestArticle(slug: takenSlug);

        var response = await client.GetSlugAvailability(takenSlug);
        var result = await response.Content.ReadFromJsonAsync<Result<bool>>();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(result!.Value).IsFalse();
    }

    [Test]
    public async Task GetSlugAvailability_ShouldReturnTrue_WhenExcludeIdOwnsSlug()
    {
        var client = await CreateAuthenticatedClientAsync();
        var ctx = ApplicationFixture.GetCmsContext();
        var ownSlug = "self-" + Guid.NewGuid().ToString("N")[..8];
        var article = ctx.BuildTestArticle(slug: ownSlug);

        var response = await client.GetSlugAvailability(ownSlug, article.Id);
        var result = await response.Content.ReadFromJsonAsync<Result<bool>>();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(result!.Value).IsTrue();
    }

    [Test]
    public async Task CreateArticle_ShouldReturn400_WhenSlugIsDuplicate()
    {
        var client = await CreateAuthenticatedClientAsync();
        var ctx = ApplicationFixture.GetCmsContext();
        var slug = "dup-" + Guid.NewGuid().ToString("N")[..8];
        ctx.BuildTestArticle(slug: slug);

        var payload = new ArticlePayload
        {
            Title = "Dup",
            Description = "Dup",
            Content = "Dup",
            Slug = slug,
        };
        var response = await client.CreateArticle(payload);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}
