using Digital.Net.Lib.Entities.Templating;
using Digital.Net.Lib.Templating.Models;
using Digital.Net.Tests.Core;

namespace Digital.Net.Tests.Lib.Entities.Templating;

public class TemplatingServiceTest : DbServiceTest<TemplatingTestContext>
{
    private ITemplateSourceResolver _first = null!;
    private ITemplateSourceResolver _second = null!;

    protected override Task OnInitializingAsync() => DbFixture.EnsureCreatedAsync<TemplatingTestContext>();

    protected override Task OnInitializedAsync()
    {
        _first = new TemplateSourceResolver<TemplatingTestContext, TemplatingTestSource>(
            Context,
            new TemplateSourceDescriptor(typeof(TemplatingTestSource), nameof(TemplatingTestSource.HostId)));
        _second = new TemplateSourceResolver<TemplatingTestContext, TemplatingTestOtherSource>(
            Context,
            new TemplateSourceDescriptor(
                typeof(TemplatingTestOtherSource),
                nameof(TemplatingTestOtherSource.HostId)));
        return Task.CompletedTask;
    }

    private void SeedFirst(Guid hostId) =>
        Context.Sources.Add(new TemplatingTestSource { Title = "First", HostId = hostId });

    private void SeedSecond(Guid hostId) =>
        Context.OtherSources.Add(new TemplatingTestOtherSource { Label = "Second", HostId = hostId });

    [Test]
    public async Task ResolveSourceAsync_Returns_Null_When_No_Resolver_Is_Declared()
    {
        var service = new TemplatingService([]);
        await Assert.That(await service.ResolveSourceAsync(Guid.NewGuid())).IsNull();
    }

    [Test]
    public async Task ResolveSourceAsync_Returns_Null_When_No_Declared_Source_Answers()
    {
        var service = new TemplatingService([_first, _second]);
        await Assert.That(await service.ResolveSourceAsync(Guid.NewGuid())).IsNull();
    }

    [Test]
    public async Task ResolveSourceAsync_Falls_Through_To_The_Next_Declared_Source()
    {
        var hostId = Guid.NewGuid();
        SeedSecond(hostId);
        await Context.SaveChangesAsync();

        var resolved = await new TemplatingService([_first, _second]).ResolveSourceAsync(hostId);

        await Assert.That(resolved).IsNotNull();
        await Assert.That(resolved!.GetCanonicalType()).IsEqualTo(typeof(TemplatingTestOtherSource));
    }

    [Test]
    public async Task ResolveSourceAsync_Stops_At_The_First_Declared_Source_That_Answers()
    {
        var hostId = Guid.NewGuid();
        SeedFirst(hostId);
        SeedSecond(hostId);
        await Context.SaveChangesAsync();

        var declaredFirst = await new TemplatingService([_first, _second]).ResolveSourceAsync(hostId);
        var declaredSecond = await new TemplatingService([_second, _first]).ResolveSourceAsync(hostId);

        await Assert.That(declaredFirst!.GetCanonicalType()).IsEqualTo(typeof(TemplatingTestSource));
        await Assert.That(declaredSecond!.GetCanonicalType()).IsEqualTo(typeof(TemplatingTestOtherSource));
    }
}