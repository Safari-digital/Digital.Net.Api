using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Digital.Net.Core.Entities.Models.Documents;
using SixLabors.ImageSharp;

namespace Digital.Net.Core.Services.Documents.Utils;

public static partial class DocumentDimensionExtractor
{
    // Accepts plain numbers ("100", "12.5") and the "px" unit ("100px"). Other CSS units are rejected.
    [GeneratedRegex(@"^\s*(\d+(?:\.\d+)?)\s*(px)?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex PixelDimensionPattern();

    public static (int? Width, int? Height) Extract(Stream stream, string mimeType)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(mimeType))
                return (null, null);
            if (DocumentTypes.SvgMimeTypes.Contains(mimeType, StringComparer.OrdinalIgnoreCase))
                return ExtractSvgDimensions(stream);
            if (!mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return (null, null);
            return ExtractBitmapDimensions(stream);
        }
        catch
        {
            return (null, null);
        }
        finally
        {
            if (stream.CanSeek)
                stream.Position = 0;
        }
    }

    private static (int? Width, int? Height) ExtractBitmapDimensions(Stream stream)
    {
        try
        {
            var info = Image.Identify(stream);
            return (info.Width, info.Height);
        }
        catch
        {
            return (null, null);
        }
    }

    private static (int? Width, int? Height) ExtractSvgDimensions(Stream stream)
    {
        try
        {
            var doc = XDocument.Load(stream);
            var root = doc.Root;
            if (root is null || !string.Equals(root.Name.LocalName, "svg", StringComparison.OrdinalIgnoreCase))
                return (null, null);

            var width = ParsePixelDimension(root.Attribute("width")?.Value);
            var height = ParsePixelDimension(root.Attribute("height")?.Value);
            if (width.HasValue && height.HasValue)
                return (width, height);

            return ExtractFromViewBox(root.Attribute("viewBox")?.Value);
        }
        catch
        {
            return (null, null);
        }
    }

    private static (int? Width, int? Height) ExtractFromViewBox(string? viewBox)
    {
        if (string.IsNullOrWhiteSpace(viewBox))
            return (null, null);

        var tokens = viewBox.Split([' ', ',', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length != 4)
            return (null, null);
        if (!double.TryParse(tokens[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var w))
            return (null, null);
        if (!double.TryParse(tokens[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var h))
            return (null, null);

        return ((int)Math.Round(w), (int)Math.Round(h));
    }

    private static int? ParsePixelDimension(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var match = PixelDimensionPattern().Match(value);
        if (!match.Success)
            return null;

        if (!double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
            return null;

        return (int)Math.Round(n);
    }
}