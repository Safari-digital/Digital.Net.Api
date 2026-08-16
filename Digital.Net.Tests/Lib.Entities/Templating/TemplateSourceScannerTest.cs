using Digital.Net.Lib.Entities.Models;
using Digital.Net.Lib.Entities.Templating;
using Digital.Net.Lib.Templating.Attributes;
using Digital.Net.Tests.Core;

namespace Digital.Net.Tests.Lib.Entities.Templating;

public class TemplateSourceScannerTest : UnitTest
{
    private class ValidSource : Entity
    {
        [TemplateHost]
        public Guid? HostId { get; set; }
    }

    private class NonNullableKeySource : Entity
    {
        [TemplateHost]
        public Guid HostId { get; set; }
    }

    private class NotASource : Entity
    {
        public Guid? HostId { get; set; }
    }

    private class TwoHostsSource : Entity
    {
        [TemplateHost]
        public Guid? FirstHostId { get; set; }

        [TemplateHost]
        public Guid? SecondHostId { get; set; }
    }

    private class StringKeySource : Entity
    {
        [TemplateHost]
        public string? HostId { get; set; }
    }

    private abstract class AbstractSource : Entity
    {
        [TemplateHost]
        public Guid? HostId { get; set; }
    }

    [Test]
    public async Task Discover_Returns_Descriptor_For_Nullable_Guid_Key()
    {
        var descriptors = TemplateSourceScanner.Discover([typeof(ValidSource)]).ToList();
        await Assert.That(descriptors.Count).IsEqualTo(1);
        await Assert.That(descriptors[0].SourceType).IsEqualTo(typeof(ValidSource));
        await Assert.That(descriptors[0].ForeignKey).IsEqualTo("HostId");
    }

    [Test]
    public async Task Discover_Returns_Descriptor_For_Non_Nullable_Guid_Key()
    {
        var descriptors = TemplateSourceScanner.Discover([typeof(NonNullableKeySource)]).ToList();
        await Assert.That(descriptors.Count).IsEqualTo(1);
        await Assert.That(descriptors[0].ForeignKey).IsEqualTo("HostId");
    }

    [Test]
    public async Task Discover_Skips_Type_Without_TemplateHost()
    {
        var descriptors = TemplateSourceScanner.Discover([typeof(NotASource)]).ToList();
        await Assert.That(descriptors).IsEmpty();
    }

    [Test]
    public async Task Discover_Skips_Non_Entity_Type()
    {
        var descriptors = TemplateSourceScanner.Discover([typeof(TemplateSourceScannerTest)]).ToList();
        await Assert.That(descriptors).IsEmpty();
    }

    [Test]
    public async Task Discover_Skips_Abstract_Entity()
    {
        var descriptors = TemplateSourceScanner.Discover([typeof(AbstractSource)]).ToList();
        await Assert.That(descriptors).IsEmpty();
    }

    [Test]
    public async Task Discover_Throws_When_Several_Properties_Carry_TemplateHost()
    {
        var exception =
            Assert.Throws<InvalidOperationException>(() =>
                TemplateSourceScanner.Discover([typeof(TwoHostsSource)]).ToList()
            );

        await Assert.That(exception!.Message).Contains(nameof(TwoHostsSource));
        await Assert.That(exception.Message).Contains("FirstHostId");
        await Assert.That(exception.Message).Contains("SecondHostId");
    }

    [Test]
    public async Task Discover_Throws_When_TemplateHost_Key_Is_Not_A_Guid()
    {
        var exception =
            Assert.Throws<InvalidOperationException>(() =>
                TemplateSourceScanner.Discover([typeof(StringKeySource)]).ToList()
            );

        await Assert.That(exception!.Message).Contains($"{nameof(StringKeySource)}.HostId");
        await Assert.That(exception.Message).Contains("String");
    }

    [Test]
    public async Task Discover_Reports_Every_Declared_Source()
    {
        var descriptors = TemplateSourceScanner
            .Discover([typeof(ValidSource), typeof(NotASource), typeof(NonNullableKeySource)])
            .ToList();

        await Assert.That(descriptors.Select(d => d.SourceType))
            .IsEquivalentTo([typeof(ValidSource), typeof(NonNullableKeySource)]);
    }
}