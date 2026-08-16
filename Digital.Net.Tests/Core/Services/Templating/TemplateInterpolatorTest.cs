using Digital.Net.Core.Services.Templating;
using Digital.Net.Lib.Entities.Attributes;
using Digital.Net.Lib.Entities.Models;

namespace Digital.Net.Tests.Core.Services.Templating;

public class TemplateInterpolatorTest : UnitTest
{
    private class TestSource : Entity
    {
        [TemplateSource]
        public string? Title { get; set; }

        [TemplateSource]
        public string? Description { get; set; }

        [TemplateSource]
        public string? PascalCaseKey { get; set; }
        public string? Hidden { get; set; }

        [TemplateTarget]
        public string? TargetOnly { get; set; }
    }

    private class TestTarget
    {
        [TemplateTarget]
        public string? Headline { get; set; }

        [TemplateTarget]
        public string? Body { get; set; }
        public string? Untouched { get; set; }

        [TemplateTarget]
        public int IgnoredNonString { get; set; }

        [TemplateSource]
        public string? SourceOnly { get; set; }
    }

    private static IReadOnlyDictionary<string, object> Sources(TestSource source) =>
        new Dictionary<string, object> { ["testsource"] = source };

    [Test]
    public async Task Interpolate_ReplacesSingleToken()
    {
        var sources = Sources(new TestSource { Title = "Hello", Description = "World" });
        var result = TemplateInterpolator.Interpolate("Page: {{ testsource.title }}", sources);
        await Assert.That(result).IsEqualTo("Page: Hello");
    }

    [Test]
    public async Task Interpolate_ReplacesMultipleOccurrences()
    {
        var sources = Sources(new TestSource { Title = "X", Description = "Y" });
        var result = TemplateInterpolator.Interpolate("{{ testsource.title }} - {{ testsource.title }}", sources);
        await Assert.That(result).IsEqualTo("X - X");
    }

    [Test]
    public async Task Interpolate_LeavesUnknownSourcePrefixUntouched()
    {
        var sources = Sources(new TestSource { Title = "X", Description = "Y" });
        var result = TemplateInterpolator.Interpolate("{{ unknown.title }}", sources);
        await Assert.That(result).IsEqualTo("{{ unknown.title }}");
    }

    [Test]
    public async Task Interpolate_LeavesNonWhitelistedFieldUntouched()
    {
        var sources = Sources(new TestSource { Title = "X", Description = "Y", Hidden = "Z" });
        var result = TemplateInterpolator.Interpolate("{{ testsource.hidden }}", sources);
        await Assert.That(result).IsEqualTo("{{ testsource.hidden }}");
    }

    [Test]
    public async Task Interpolate_LeavesUnknownFieldUntouched()
    {
        var sources = Sources(new TestSource { Title = "X", Description = "Y" });
        var result = TemplateInterpolator.Interpolate("{{ testsource.nope }}", sources);
        await Assert.That(result).IsEqualTo("{{ testsource.nope }}");
    }

    [Test]
    public async Task Interpolate_TreatsNullSourceFieldAsEmptyString()
    {
        var sources = Sources(new TestSource { Title = null, Description = "D" });
        var result = TemplateInterpolator.Interpolate(">{{ testsource.title }}<", sources);
        await Assert.That(result).IsEqualTo("><");
    }

    [Test]
    public async Task Interpolate_DoesNotMatchSingleBraces_InJsonContent()
    {
        var sources = Sources(new TestSource { Title = "Hello", Description = null });
        const string template = "{\"@type\":\"Article\",\"headline\":\"{{ testsource.title }}\",\"meta\":{\"k\":\"v\"}}";
        var result = TemplateInterpolator.Interpolate(template, sources);
        await Assert.That(result).IsEqualTo("{\"@type\":\"Article\",\"headline\":\"Hello\",\"meta\":{\"k\":\"v\"}}");
    }

    [Test]
    public async Task Interpolate_NullTemplate_ReturnsNull()
    {
        var result = TemplateInterpolator.Interpolate(null, new Dictionary<string, object>());
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Interpolate_EmptyTemplate_ReturnsEmpty()
    {
        var result = TemplateInterpolator.Interpolate(string.Empty, new Dictionary<string, object>());
        await Assert.That(result).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task Interpolate_ToleratesWhitespace()
    {
        var sources = Sources(new TestSource { Title = "X", Description = "Y" });
        var noSpace = TemplateInterpolator.Interpolate("{{testsource.title}}", sources);
        var lotsOfSpace = TemplateInterpolator.Interpolate("{{   testsource.title   }}", sources);
        await Assert.That(noSpace).IsEqualTo("X");
        await Assert.That(lotsOfSpace).IsEqualTo("X");
    }

    [Test]
    public async Task Interpolate_Fallback_UsesTheFirstTermThatHoldsAValue()
    {
        var sources = Sources(new TestSource { Title = "Hello", Description = "World" });
        var result = TemplateInterpolator.Interpolate("{{ testsource.title ?? testsource.description }}", sources);
        await Assert.That(result).IsEqualTo("Hello");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Interpolate_Fallback_SkipsNullAndBlankTerms(string? empty)
    {
        var sources = Sources(new TestSource { Title = empty, Description = "World" });
        var result = TemplateInterpolator.Interpolate(
            "{{ testsource.title ?? testsource.description }}",
            sources
        );
        await Assert.That(result).IsEqualTo("World");
    }

    [Test]
    public async Task Interpolate_Fallback_WalksMoreThanTwoTerms()
    {
        var sources = Sources(new TestSource { Title = null, Description = null, Hidden = "Z" });
        var result = TemplateInterpolator.Interpolate(
            "{{ testsource.title ?? testsource.description ?? testsource.title }}",
            sources
        );
        await Assert.That(result).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task Interpolate_Fallback_SkipsUnknownTerms_AndKeepsGoing()
    {
        var sources = Sources(new TestSource { Title = "Hello", Description = "World" });
        var result = TemplateInterpolator.Interpolate(
            "{{ unknown.field ?? testsource.hidden ?? testsource.description }}",
            sources
        );
        await Assert.That(result).IsEqualTo("World");
    }

    [Test]
    public async Task Interpolate_Fallback_LeavesTokenVerbatim_WhenNoTermIsKnown()
    {
        var sources = Sources(new TestSource { Title = "X", Description = "Y", Hidden = "Z" });
        const string template = "{{ testsource.hidden ?? unknown.field }}";
        var result = TemplateInterpolator.Interpolate(template, sources);
        await Assert.That(result).IsEqualTo(template);
    }

    [Test]
    [Arguments("{{ testsource.tItle }} {{testsOUrce.DESCRIPTION}}{{testsource.pascalcasekey}}")]
    [Arguments("{{ testsource.title }} {{TESTSOURCe.dESCRIPTION}}{{testsOurce.pascalcasekey}}")]
    [Arguments("{{ testsource.Title }} {{TestSource.DESCRIPTION}}{{testsource.pascalcasekey}}")]
    public async Task Interpolate_MatchesTheKeyAndSourceAliases_WhateverItsCase(string value)
    {
        var sources = Sources(new TestSource { Title = "Hello", Description = "World", PascalCaseKey = "!" });
        const string expected = "Hello World!";
        await Assert.That(TemplateInterpolator.Interpolate(value, sources)).IsEqualTo(expected);
    }

    [Test]
    public async Task Interpolate_ToleratesWhitespaceAroundTheFallbackOperator()
    {
        var sources = Sources(new TestSource { Title = null, Description = "World" });
        var tight = TemplateInterpolator.Interpolate("{{testsource.title??testsource.description}}", sources);
        var loose = TemplateInterpolator.Interpolate("{{  testsource.title   ??   testsource.description  }}", sources);
        await Assert.That(tight).IsEqualTo("World");
        await Assert.That(loose).IsEqualTo("World");
    }

    [Test]
    public async Task GetVariables_ReturnsOnlySourceProperties()
    {
        var variables = TemplateInterpolator.GetVariables<TestSource>();
        await Assert.That(variables.Count).IsEqualTo(2);
        await Assert.That(variables.Any(v => v.Field == "Title" && v.Token == "{{ testsource.title }}")).IsTrue();
        await Assert.That(variables.Any(v => v.Field == "Description")).IsTrue();
        await Assert.That(variables.Any(v => v.Field == "Hidden")).IsFalse();
        await Assert.That(variables.Any(v => v.Field == "TargetOnly")).IsFalse();
    }

    [Test]
    public async Task Interpolate_LeavesTargetOnlyFieldUntouched()
    {
        var sources = Sources(new TestSource { Title = "X", Description = "Y", TargetOnly = "Z" });
        var result = TemplateInterpolator.Interpolate("{{ testsource.targetonly }}", sources);
        await Assert.That(result).IsEqualTo("{{ testsource.targetonly }}");
    }

    [Test]
    public async Task Interpolate_LeavesSourceOnlyPropertyUntouched()
    {
        var sources = Sources(new TestSource { Title = "Foo", Description = "Bar" });
        var target = new TestTarget { SourceOnly = "{{ testsource.title }}" };
        TemplateInterpolator.Interpolate(target, sources);
        await Assert.That(target.SourceOnly).IsEqualTo("{{ testsource.title }}");
    }

    [Test]
    public async Task Interpolate_RewritesAllTargetStrings()
    {
        var sources = Sources(new TestSource { Title = "Foo", Description = "Bar" });
        var target = new TestTarget
        {
            Headline = "{{ testsource.title }}",
            Body = "[{{ testsource.description }}]",
            Untouched = "{{ testsource.title }}",
            IgnoredNonString = 7
        };

        TemplateInterpolator.Interpolate(target, sources);

        await Assert.That(target.Headline).IsEqualTo("Foo");
        await Assert.That(target.Body).IsEqualTo("[Bar]");
        await Assert.That(target.Untouched).IsEqualTo("{{ testsource.title }}");
        await Assert.That(target.IgnoredNonString).IsEqualTo(7);
    }

    [Test]
    public async Task Interpolate_LeavesNullPropertiesUntouched()
    {
        var sources = Sources(new TestSource { Title = "Foo", Description = "Bar" });
        var target = new TestTarget { Headline = null, Body = null };
        TemplateInterpolator.Interpolate(target, sources);
        await Assert.That(target.Headline).IsNull();
        await Assert.That(target.Body).IsNull();
    }
}
