using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Digital.Net.Core.Services.Documents.Utils;

public static partial class SvgSanitizer
{
    private static readonly HashSet<string> DangerousElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "foreignObject", "iframe", "embed", "object", "applet",
        "meta", "link", "style", "handler", "set", "animate"
    };

    private static readonly HashSet<string> DangerousAttributePrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "on" // onclick, onload, onerror, onmouseover...
    };

    private static readonly HashSet<string> DangerousAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "xlink:href", "href"
    };

    [GeneratedRegex(@"^\s*(javascript|data|vbscript):", RegexOptions.IgnoreCase)]
    private static partial Regex DangerousUriScheme();

    public static async Task<Stream> SanitizeAsync(Stream input)
    {
        using var reader = new StreamReader(input);
        var content = await reader.ReadToEndAsync();

        var doc = XDocument.Parse(content);
        SanitizeElement(doc.Root!);

        var sanitized = new MemoryStream();
        await doc.SaveAsync(sanitized, SaveOptions.DisableFormatting, CancellationToken.None);
        sanitized.Position = 0;
        return sanitized;
    }

    private static void SanitizeElement(XElement element)
    {
        var localName = element.Name.LocalName;
        if (DangerousElements.Contains(localName))
        {
            element.Remove();
            return;
        }

        var attributesToRemove = element.Attributes()
            .Where(a =>
            {
                var attrName = a.Name.LocalName;
                if (DangerousAttributePrefixes.Any(p => attrName.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                    return true;
                if (DangerousAttributes.Contains(attrName) && DangerousUriScheme().IsMatch(a.Value))
                    return true;
                return false;
            })
            .ToList();

        foreach (var attr in attributesToRemove)
            attr.Remove();

        foreach (var child in element.Elements().ToList())
            SanitizeElement(child);
    }
}
