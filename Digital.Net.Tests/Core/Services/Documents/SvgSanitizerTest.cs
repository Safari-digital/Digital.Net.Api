using System.Text;
using System.Xml.Linq;
using Digital.Net.Core.Services.Documents.Utils;

namespace Digital.Net.Tests.Core.Services.Documents;

public class SvgSanitizerTest : UnitTest
{
    private static Stream CreateSvgStream(string svgContent) =>
        new MemoryStream(Encoding.UTF8.GetBytes(svgContent));

    private static async Task<string> SanitizeAndRead(string svgContent)
    {
        await using var input = CreateSvgStream(svgContent);
        var result = await SvgSanitizer.SanitizeAsync(input);
        using var reader = new StreamReader(result);
        return await reader.ReadToEndAsync();
    }

    [Test]
    public async Task SanitizeAsync_ShouldRemoveScriptElement()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert('xss')</script><rect width=\"10\" height=\"10\"/></svg>";
        var result = await SanitizeAndRead(svg);
        var doc = XDocument.Parse(result);
        await Assert.That(doc.Descendants().Any(e => e.Name.LocalName == "script")).IsFalse();
        await Assert.That(doc.Descendants().Any(e => e.Name.LocalName == "rect")).IsTrue();
    }

    [Test]
    public async Task SanitizeAsync_ShouldRemoveForeignObjectElement()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><foreignObject><body xmlns=\"http://www.w3.org/1999/xhtml\"><div>hack</div></body></foreignObject></svg>";
        var result = await SanitizeAndRead(svg);
        var doc = XDocument.Parse(result);
        await Assert.That(doc.Descendants().Any(e => e.Name.LocalName == "foreignObject")).IsFalse();
    }

    [Test]
    public async Task SanitizeAsync_ShouldRemoveIframeElement()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><iframe src=\"https://evil.com\"/></svg>";
        var result = await SanitizeAndRead(svg);
        var doc = XDocument.Parse(result);
        await Assert.That(doc.Descendants().Any(e => e.Name.LocalName == "iframe")).IsFalse();
    }

    [Test]
    public async Task SanitizeAsync_ShouldRemoveEmbedElement()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><embed src=\"evil.swf\"/></svg>";
        var result = await SanitizeAndRead(svg);
        var doc = XDocument.Parse(result);
        await Assert.That(doc.Descendants().Any(e => e.Name.LocalName == "embed")).IsFalse();
    }

    [Test]
    public async Task SanitizeAsync_ShouldRemoveObjectElement()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><object data=\"evil.swf\"/></svg>";
        var result = await SanitizeAndRead(svg);
        var doc = XDocument.Parse(result);
        await Assert.That(doc.Descendants().Any(e => e.Name.LocalName == "object")).IsFalse();
    }

    [Test]
    public async Task SanitizeAsync_ShouldRemoveStyleElement()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><style>body { display: none; }</style><rect width=\"10\" height=\"10\"/></svg>";
        var result = await SanitizeAndRead(svg);
        var doc = XDocument.Parse(result);
        await Assert.That(doc.Descendants().Any(e => e.Name.LocalName == "style")).IsFalse();
    }

    [Test]
    public async Task SanitizeAsync_ShouldRemoveOnclickAttribute()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect onclick=\"alert('xss')\" width=\"10\" height=\"10\"/></svg>";
        var result = await SanitizeAndRead(svg);
        var doc = XDocument.Parse(result);
        var rect = doc.Descendants().First(e => e.Name.LocalName == "rect");
        await Assert.That(rect.Attribute("onclick")).IsNull();
    }

    [Test]
    public async Task SanitizeAsync_ShouldRemoveOnloadAttribute()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" onload=\"alert('xss')\"><rect width=\"10\" height=\"10\"/></svg>";
        var result = await SanitizeAndRead(svg);
        var doc = XDocument.Parse(result);
        await Assert.That(doc.Root!.Attribute("onload")).IsNull();
    }

    [Test]
    public async Task SanitizeAsync_ShouldRemoveOnErrorAttribute()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><image onerror=\"alert('xss')\"/></svg>";
        var result = await SanitizeAndRead(svg);
        var doc = XDocument.Parse(result);
        var image = doc.Descendants().First(e => e.Name.LocalName == "image");
        await Assert.That(image.Attribute("onerror")).IsNull();
    }

    [Test]
    public async Task SanitizeAsync_ShouldRemoveJavascriptHref()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><a href=\"javascript:alert('xss')\"><text>click</text></a></svg>";
        var result = await SanitizeAndRead(svg);
        var doc = XDocument.Parse(result);
        var anchor = doc.Descendants().First(e => e.Name.LocalName == "a");
        await Assert.That(anchor.Attribute("href")).IsNull();
    }

    [Test]
    public async Task SanitizeAsync_ShouldRemoveDataUriHref()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><a href=\"data:text/html,malicious\"><text>click</text></a></svg>";
        var result = await SanitizeAndRead(svg);
        var doc = XDocument.Parse(result);
        var anchor = doc.Descendants().First(e => e.Name.LocalName == "a");
        await Assert.That(anchor.Attribute("href")).IsNull();
    }

    [Test]
    public async Task SanitizeAsync_ShouldRemoveVbscriptHref()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><a href=\"vbscript:MsgBox('xss')\"><text>click</text></a></svg>";
        var result = await SanitizeAndRead(svg);
        var doc = XDocument.Parse(result);
        var anchor = doc.Descendants().First(e => e.Name.LocalName == "a");
        await Assert.That(anchor.Attribute("href")).IsNull();
    }

    [Test]
    public async Task SanitizeAsync_ShouldKeepSafeHref()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><a href=\"https://safe.com\"><text>click</text></a></svg>";
        var result = await SanitizeAndRead(svg);
        var doc = XDocument.Parse(result);
        var anchor = doc.Descendants().First(e => e.Name.LocalName == "a");
        await Assert.That(anchor.Attribute("href")).IsNotNull();
        await Assert.That(anchor.Attribute("href")!.Value).IsEqualTo("https://safe.com");
    }

    [Test]
    public async Task SanitizeAsync_ShouldKeepSafeElements()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect width=\"100\" height=\"100\" fill=\"red\"/><circle cx=\"50\" cy=\"50\" r=\"40\"/><text x=\"10\" y=\"20\">Hello</text></svg>";
        var result = await SanitizeAndRead(svg);
        var doc = XDocument.Parse(result);
        await Assert.That(doc.Descendants().Any(e => e.Name.LocalName == "rect")).IsTrue();
        await Assert.That(doc.Descendants().Any(e => e.Name.LocalName == "circle")).IsTrue();
        await Assert.That(doc.Descendants().Any(e => e.Name.LocalName == "text")).IsTrue();
    }

    [Test]
    public async Task SanitizeAsync_ShouldKeepSafeAttributes()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect width=\"10\" height=\"10\" fill=\"blue\" stroke=\"red\" id=\"myRect\" class=\"box\"/></svg>";
        var result = await SanitizeAndRead(svg);
        var doc = XDocument.Parse(result);
        var rect = doc.Descendants().First(e => e.Name.LocalName == "rect");
        await Assert.That(rect.Attribute("fill")!.Value).IsEqualTo("blue");
        await Assert.That(rect.Attribute("id")!.Value).IsEqualTo("myRect");
    }

    [Test]
    public async Task SanitizeAsync_ShouldRemoveNestedDangerousElements()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><g><g><script>alert('deep')</script></g></g><rect width=\"10\" height=\"10\"/></svg>";
        var result = await SanitizeAndRead(svg);
        var doc = XDocument.Parse(result);
        await Assert.That(doc.Descendants().Any(e => e.Name.LocalName == "script")).IsFalse();
        await Assert.That(doc.Descendants().Any(e => e.Name.LocalName == "rect")).IsTrue();
    }

    [Test]
    public async Task SanitizeAsync_ShouldRemoveMultipleDangerousElements()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>1</script><iframe/><embed/><object/><rect width=\"10\" height=\"10\"/></svg>";
        var result = await SanitizeAndRead(svg);
        var doc = XDocument.Parse(result);
        await Assert.That(doc.Descendants().Any(e => e.Name.LocalName == "script")).IsFalse();
        await Assert.That(doc.Descendants().Any(e => e.Name.LocalName == "iframe")).IsFalse();
        await Assert.That(doc.Descendants().Any(e => e.Name.LocalName == "embed")).IsFalse();
        await Assert.That(doc.Descendants().Any(e => e.Name.LocalName == "object")).IsFalse();
        await Assert.That(doc.Descendants().Any(e => e.Name.LocalName == "rect")).IsTrue();
    }

    [Test]
    public async Task SanitizeAsync_ShouldHandleCleanSvgWithoutChanges()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect width=\"100\" height=\"100\" fill=\"green\"/></svg>";
        var result = await SanitizeAndRead(svg);
        var doc = XDocument.Parse(result);
        await Assert.That(doc.Descendants().Any(e => e.Name.LocalName == "rect")).IsTrue();
        var rect = doc.Descendants().First(e => e.Name.LocalName == "rect");
        await Assert.That(rect.Attribute("fill")!.Value).IsEqualTo("green");
    }
}
