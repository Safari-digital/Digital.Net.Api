using Digital.Net.Lib.Entities.Templating;
using Digital.Net.Lib.Templating.Models;
using Digital.Net.Tests.Core;

namespace Digital.Net.Tests.Lib.Entities.Templating;

public class TemplateSourceResolverTest : DbServiceTest<TemplatingTestContext>
{
    private TemplateSourceResolver<TemplatingTestContext, TemplatingTestSource> _resolver = null!;

    protected override Task OnInitializingAsync() => DbFixture.EnsureCreatedAsync<TemplatingTestContext>();

    protected override Task OnInitializedAsync()
    {
        _resolver = new TemplateSourceResolver<TemplatingTestContext, TemplatingTestSource>(
            Context,
            new TemplateSourceDescriptor(typeof(TemplatingTestSource), nameof(TemplatingTestSource.HostId))
        );
        return Task.CompletedTask;
    }

    private async Task<TemplatingTestSource> SeedAsync(Guid? hostId, string title = "Title")
    {
        var source = new TemplatingTestSource { Title = title, HostId = hostId };
        Context.Sources.Add(source);
        await Context.SaveChangesAsync();
        return source;
    }

    [Test]
    public async Task ResolveAsync_Returns_Source_Attached_To_Host()
    {
        var hostId = Guid.NewGuid();
        var seeded = await SeedAsync(hostId, "Attached");

        var resolved = await _resolver.ResolveAsync(hostId);

        await Assert.That(resolved).IsNotNull();
        await Assert.That(resolved!.Id).IsEqualTo(seeded.Id);
    }

    [Test]
    public async Task ResolveAsync_Returns_Null_When_No_Source_Is_Attached()
    {
        await SeedAsync(Guid.NewGuid());
        var resolved = await _resolver.ResolveAsync(Guid.NewGuid());
        await Assert.That(resolved).IsNull();
    }

    [Test]
    public async Task ResolveAsync_Ignores_Unattached_Source()
    {
        await SeedAsync(null, "Orphan");
        var resolved = await _resolver.ResolveAsync(Guid.NewGuid());
        await Assert.That(resolved).IsNull();
    }

    [Test]
    public async Task ResolveAsync_Reports_The_Declared_Source_Type() =>
        await Assert.That(_resolver.SourceType).IsEqualTo(typeof(TemplatingTestSource));
}