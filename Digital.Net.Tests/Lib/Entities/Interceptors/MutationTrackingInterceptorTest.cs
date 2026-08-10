using Digital.Net.Cms.Context;
using Digital.Net.Cms.Models;
using Digital.Net.Core.Entities.Context;
using Digital.Net.Core.Entities.Models.Auth;
using Digital.Net.Core.Entities.Models.Documents;
using Digital.Net.Core.Entities.Models.Users;
using Digital.Net.Lib.Accessors;
using Digital.Net.Lib.Entities.Context;
using Digital.Net.Lib.Entities.Interceptors;
using Digital.Net.Lib.Entities.Mutations;
using Digital.Net.Tests.Core;
using Digital.Net.Tests.Core.Factories;
using Digital.Net.Tests.Core.Factories.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Digital.Net.Tests.Lib.Entities.Interceptors;

public class MutationTrackingInterceptorTest : UnitTest
{
    [ClassDataSource<DatabaseFixture>]
    public required DatabaseFixture DbFixture { get; init; }

    private static readonly Guid StubUserId = Guid.NewGuid();
    private const string StubIp = "203.0.113.7";
    private const string StubUserAgent = "MutationTrackingTest/1.0";

    [Test]
    public async Task Create_TrackedEntity_PersistsCreatedMutation_WithOriginAndUser()
    {
        await using var ctx = CreateTrackedContext<DigitalContext>();
        var document = NewDocument();

        ctx.Add(document);
        await ctx.SaveChangesAsync();

        var mutation = await ReadMutation<DigitalContext>(document.Id);
        await Assert.That(mutation).IsNotNull();
        await Assert.That(mutation!.ChangeType).IsEqualTo(ChangeType.Created);
        await Assert.That(mutation.EntityType).IsEqualTo(nameof(Document));
        await Assert.That(mutation.UserId).IsEqualTo(StubUserId);
        await Assert.That(mutation.IpAddress).IsEqualTo(StubIp);
        await Assert.That(mutation.UserAgent).IsEqualTo(StubUserAgent);
    }

    [Test]
    public async Task Update_TrackedEntity_PersistsUpdatedMutation()
    {
        await using var ctx = CreateTrackedContext<DigitalContext>();
        var document = NewDocument();
        ctx.Add(document);
        await ctx.SaveChangesAsync();

        document.FileSize += 1;
        await ctx.SaveChangesAsync();

        var mutations = await ReadMutations<DigitalContext>(document.Id, ChangeType.Updated);
        await Assert.That(mutations.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Delete_TrackedEntity_PersistsDeletedMutation()
    {
        await using var ctx = CreateTrackedContext<DigitalContext>();
        var document = NewDocument();
        ctx.Add(document);
        await ctx.SaveChangesAsync();

        ctx.Remove(document);
        await ctx.SaveChangesAsync();

        var mutations = await ReadMutations<DigitalContext>(document.Id, ChangeType.Deleted);
        await Assert.That(mutations.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Save_UntrackedEntity_PersistsNoMutation()
    {
        await using var ctx = CreateTrackedContext<DigitalContext>();
        var authEvent = new AuthEvent { Type = AuthEventType.Login, Success = false };

        ctx.Add(authEvent);
        await ctx.SaveChangesAsync();

        var mutation = await ReadMutation<DigitalContext>(authEvent.Id);
        await Assert.That(mutation).IsNull();
    }

    [Test]
    public async Task Create_TrackedEntity_InCmsContext_PersistsMutationInCmsSchema()
    {
        await using var ctx = CreateTrackedContext<CmsContext>();
        var tag = new Tag { Name = $"tag-{TestId}" };

        ctx.Add(tag);
        await ctx.SaveChangesAsync();

        var mutation = await ReadMutation<CmsContext>(tag.Id);
        await Assert.That(mutation).IsNotNull();
        await Assert.That(mutation!.ChangeType).IsEqualTo(ChangeType.Created);
        await Assert.That(mutation.EntityType).IsEqualTo(nameof(Tag));
    }

    [Test]
    public async Task Create_CapturesInitialValuesInPayload()
    {
        await using var ctx = CreateTrackedContext<DigitalContext>();
        var document = NewDocument();

        ctx.Add(document);
        await ctx.SaveChangesAsync();

        var mutation = await ReadMutation<DigitalContext>(document.Id);
        await Assert.That(mutation!.Payload).IsNotNull();
        await Assert.That(mutation.Payload!.Contains(nameof(Document.FileName))).IsTrue();
        await Assert.That(mutation.Payload!.Contains(nameof(Document.FileSize))).IsTrue();
    }

    [Test]
    public async Task Update_CapturesOnlyChangedFieldDiff()
    {
        await using var ctx = CreateTrackedContext<DigitalContext>();
        var document = NewDocument();
        ctx.Add(document);
        await ctx.SaveChangesAsync();

        document.FileSize = 999;
        await ctx.SaveChangesAsync();

        var mutation = (await ReadMutations<DigitalContext>(document.Id, ChangeType.Updated)).Single();
        await Assert.That(mutation.Payload).IsNotNull();
        await Assert.That(mutation.Payload!.Contains(nameof(Document.FileSize))).IsTrue();
        await Assert.That(mutation.Payload!.Contains(nameof(Document.MimeType))).IsFalse();
    }

    [Test]
    public async Task Payload_ExcludesSecretFields()
    {
        await using var ctx = CreateTrackedContext<DigitalContext>();
        var user = ctx.BuildTestUser();

        var mutation = (await ReadMutations<DigitalContext>(user.Id, ChangeType.Created)).Single();
        await Assert.That(mutation.Payload).IsNotNull();
        await Assert.That(mutation.Payload!.Contains(nameof(User.Username))).IsTrue();
        await Assert.That(mutation.Payload!.Contains(nameof(User.Password))).IsFalse();
    }

    [Test]
    public async Task Create_Sheet_IsTracked()
    {
        await using var ctx = CreateTrackedContext<CmsContext>();
        var sheet = NewSheet("<p>hello</p>");

        ctx.Add(sheet);
        await ctx.SaveChangesAsync();

        var mutation = await ReadMutation<CmsContext>(sheet.Id);
        await Assert.That(mutation).IsNotNull();
        await Assert.That(mutation!.EntityType).IsEqualTo(nameof(Sheet));
    }

    [Test]
    public async Task Payload_TruncatesOversizedValue_AndKeepsAReadablePrefix()
    {
        await using var ctx = CreateTrackedContext<CmsContext>();
        var content = new string('a', 2000);
        var sheet = NewSheet(content);

        ctx.Add(sheet);
        await ctx.SaveChangesAsync();

        var mutation = await ReadMutation<CmsContext>(sheet.Id);
        await Assert.That(mutation!.Payload).IsNotNull();
        await Assert.That(mutation.Payload!.Contains(MutationTrackingInterceptor.TruncationMarker)).IsTrue();
        await Assert.That(mutation.Payload!.Contains("2000 chars")).IsTrue();
        // The kept prefix is what makes the trail usable on editorial content.
        await Assert.That(mutation.Payload!.Contains(new string('a', 512))).IsTrue();
        await Assert.That(mutation.Payload!.Contains(content)).IsFalse();
    }

    [Test]
    public async Task Payload_LeavesValueUnderThreshold_Untouched()
    {
        await using var ctx = CreateTrackedContext<CmsContext>();
        // No angle brackets: the default JSON encoder escapes them, which would make the assertion
        // about escaping rather than about truncation.
        var content = $"short-{TestId}";
        var sheet = NewSheet(content);

        ctx.Add(sheet);
        await ctx.SaveChangesAsync();

        var mutation = await ReadMutation<CmsContext>(sheet.Id);
        await Assert.That(mutation!.Payload!.Contains(content)).IsTrue();
        await Assert.That(mutation.Payload!.Contains(MutationTrackingInterceptor.TruncationMarker)).IsFalse();
    }

    private Sheet NewSheet(string content) => new()
    {
        Name = $"sheet-{TestId}-{Guid.NewGuid():N}"[..32],
        Type = "html",
        Content = content,
        Published = true
    };

    private T CreateTrackedContext<T>() where T : DbContext
    {
        var provider = new ServiceCollection()
            .AddSingleton<IOriginAccessor>(new StubOriginAccessor(StubIp, StubUserAgent))
            .AddSingleton<IUserAccessor>(new StubUserAccessor(StubUserId))
            .AddSingleton(new MutationBroadcaster(NullLogger<MutationBroadcaster>.Instance))
            .BuildServiceProvider();

        var options = new DbContextOptionsBuilder<T>()
            .UseDigitalNpgsql<T>(DbFixture.Fixture.ConnectionString)
            .AddInterceptors(new MutationTrackingInterceptor(provider))
            .Options;

        return (T)Activator.CreateInstance(typeof(T), options)!;
    }

    private async Task<EntityMutation?> ReadMutation<T>(Guid entityId) where T : DbContext
    {
        await using var read = DbFixture.CreateContext<T>();
        return await read.Set<EntityMutation>().SingleOrDefaultAsync(m => m.EntityId == entityId);
    }

    private async Task<List<EntityMutation>> ReadMutations<T>(Guid entityId, ChangeType changeType) where T : DbContext
    {
        await using var read = DbFixture.CreateContext<T>();
        return await read.Set<EntityMutation>()
            .Where(m => m.EntityId == entityId && m.ChangeType == changeType)
            .ToListAsync();
    }

    private static Document NewDocument() => new()
    {
        FileName = $"mut-{Guid.NewGuid():N}.bin",
        MimeType = "application/octet-stream",
        FileSize = 1
    };

    private sealed class StubOriginAccessor(string? ip, string? userAgent) : IOriginAccessor
    {
        public RequestOrigin GetOrigin() => new(ip, userAgent);
        public RequestOrigin TryGetOrigin() => new(ip, userAgent);
    }

    private sealed class StubUserAccessor(Guid? userId) : IUserAccessor
    {
        public Guid GetUserId() => userId ?? throw new InvalidOperationException();
        public Guid? TryGetUserId() => userId;
    }
}
