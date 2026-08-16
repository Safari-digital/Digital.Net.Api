using System.Text;
using Digital.Net.Core.Services.Documents;
using Digital.Net.Core.Services.Documents.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;

namespace Digital.Net.Tests.Core.Services.Documents;

public class DocumentDimensionExtractorTest : UnitTest
{
    private static MemoryStream CreateImageStream<TFormat>(int width, int height, TFormat encoder)
        where TFormat : IImageEncoder
    {
        using var image = new Image<Rgba32>(width, height);
        var stream = new MemoryStream();
        image.Save(stream, encoder);
        stream.Position = 0;
        return stream;
    }

    private static MemoryStream CreateTextStream(string content) => new(Encoding.UTF8.GetBytes(content));

    [Test]
    public async Task Extract_Png_ReturnsCorrectDimensions()
    {
        using var stream = CreateImageStream(320, 200, new PngEncoder());
        var (width, height) = DocumentDimensionExtractor.Extract(stream, "image/png");
        await Assert.That(width).IsEqualTo(320);
        await Assert.That(height).IsEqualTo(200);
    }

    [Test]
    public async Task Extract_Jpeg_ReturnsCorrectDimensions()
    {
        using var stream = CreateImageStream(640, 480, new JpegEncoder());
        var (width, height) = DocumentDimensionExtractor.Extract(stream, "image/jpeg");
        await Assert.That(width).IsEqualTo(640);
        await Assert.That(height).IsEqualTo(480);
    }

    [Test]
    public async Task Extract_Webp_ReturnsCorrectDimensions()
    {
        using var stream = CreateImageStream(150, 75, new WebpEncoder());
        var (width, height) = DocumentDimensionExtractor.Extract(stream, "image/webp");
        await Assert.That(width).IsEqualTo(150);
        await Assert.That(height).IsEqualTo(75);
    }

    [Test]
    public async Task Extract_Gif_ReturnsCorrectDimensions()
    {
        using var stream = CreateImageStream(50, 25, new GifEncoder());
        var (width, height) = DocumentDimensionExtractor.Extract(stream, "image/gif");
        await Assert.That(width).IsEqualTo(50);
        await Assert.That(height).IsEqualTo(25);
    }

    [Test]
    public async Task Extract_Svg_WithNumericWidthHeight_ReturnsDimensions()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"100\" height=\"50\"></svg>";
        using var stream = CreateTextStream(svg);

        var (width, height) = DocumentDimensionExtractor.Extract(stream, "image/svg+xml");
        await Assert.That(width).IsEqualTo(100);
        await Assert.That(height).IsEqualTo(50);
    }

    [Test]
    public async Task Extract_Svg_WithPxUnit_ReturnsDimensions()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"100px\" height=\"50px\"></svg>";
        using var stream = CreateTextStream(svg);

        var (width, height) = DocumentDimensionExtractor.Extract(stream, "image/svg+xml");
        await Assert.That(width).IsEqualTo(100);
        await Assert.That(height).IsEqualTo(50);
    }

    [Test]
    public async Task Extract_Svg_WithPercentUnit_ReturnsNull()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"100%\" height=\"50%\"></svg>";
        using var stream = CreateTextStream(svg);

        var (width, height) = DocumentDimensionExtractor.Extract(stream, "image/svg+xml");
        await Assert.That(width).IsNull();
        await Assert.That(height).IsNull();
    }

    [Test]
    public async Task Extract_Svg_WithEmUnit_ReturnsNull()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10em\" height=\"5em\"></svg>";
        using var stream = CreateTextStream(svg);

        var (width, height) = DocumentDimensionExtractor.Extract(stream, "image/svg+xml");
        await Assert.That(width).IsNull();
        await Assert.That(height).IsNull();
    }

    [Test]
    public async Task Extract_Svg_WithViewBoxOnly_ReturnsViewBoxDimensions()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 200 100\"></svg>";
        using var stream = CreateTextStream(svg);

        var (width, height) = DocumentDimensionExtractor.Extract(stream, "image/svg+xml");
        await Assert.That(width).IsEqualTo(200);
        await Assert.That(height).IsEqualTo(100);
    }

    [Test]
    public async Task Extract_Svg_WithPercentWidthAndViewBox_FallsBackToViewBox()
    {
        var svg =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"100%\" height=\"100%\" viewBox=\"0 0 800 600\"></svg>";
        using var stream = CreateTextStream(svg);

        var (width, height) = DocumentDimensionExtractor.Extract(stream, "image/svg+xml");
        await Assert.That(width).IsEqualTo(800);
        await Assert.That(height).IsEqualTo(600);
    }

    [Test]
    public async Task Extract_Svg_WithoutDimensionsOrViewBox_ReturnsNull()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect/></svg>";
        using var stream = CreateTextStream(svg);

        var (width, height) = DocumentDimensionExtractor.Extract(stream, "image/svg+xml");
        await Assert.That(width).IsNull();
        await Assert.That(height).IsNull();
    }

    [Test]
    public async Task Extract_Svg_Malformed_ReturnsNull()
    {
        using var stream = CreateTextStream("not xml at all");

        var (width, height) = DocumentDimensionExtractor.Extract(stream, "image/svg+xml");
        await Assert.That(width).IsNull();
        await Assert.That(height).IsNull();
    }

    [Test]
    public async Task Extract_Svg_WithDecimalViewBox_RoundsToNearestInt()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100.6 50.4\"></svg>";
        using var stream = CreateTextStream(svg);

        var (width, height) = DocumentDimensionExtractor.Extract(stream, "image/svg+xml");
        await Assert.That(width).IsEqualTo(101);
        await Assert.That(height).IsEqualTo(50);
    }

    [Test]
    public async Task Extract_CorruptedBitmap_ReturnsNull()
    {
        var randomBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0xFF, 0xAA, 0x00, 0x11 };
        using var stream = new MemoryStream(randomBytes);

        var (width, height) = DocumentDimensionExtractor.Extract(stream, "image/png");
        await Assert.That(width).IsNull();
        await Assert.That(height).IsNull();
    }

    [Test]
    public async Task Extract_EmptyStream_ReturnsNull()
    {
        using var stream = new MemoryStream();

        var (width, height) = DocumentDimensionExtractor.Extract(stream, "image/png");
        await Assert.That(width).IsNull();
        await Assert.That(height).IsNull();
    }

    [Test]
    public async Task Extract_NonImageMimeType_ReturnsNull()
    {
        using var stream = CreateImageStream(100, 100, new PngEncoder());

        var (width, height) = DocumentDimensionExtractor.Extract(stream, "application/pdf");
        await Assert.That(width).IsNull();
        await Assert.That(height).IsNull();
    }

    [Test]
    public async Task Extract_EmptyMimeType_ReturnsNull()
    {
        using var stream = CreateImageStream(100, 100, new PngEncoder());

        var (width, height) = DocumentDimensionExtractor.Extract(stream, "");
        await Assert.That(width).IsNull();
        await Assert.That(height).IsNull();
    }

    [Test]
    public async Task Extract_AfterCall_StreamIsRepositionedToZero()
    {
        using var stream = CreateImageStream(100, 50, new PngEncoder());
        DocumentDimensionExtractor.Extract(stream, "image/png");
        await Assert.That(stream.Position).IsEqualTo(0L);
    }

    [Test]
    public async Task Extract_AfterFailedExtraction_StreamIsRepositionedToZero()
    {
        var randomBytes = new byte[] { 0xFF, 0xAA, 0x00, 0x11 };
        using var stream = new MemoryStream(randomBytes);
        // Advance position so we can detect repositioning even on failure
        stream.Position = 2;
        DocumentDimensionExtractor.Extract(stream, "image/png");
        await Assert.That(stream.Position).IsEqualTo(0L);
    }
}