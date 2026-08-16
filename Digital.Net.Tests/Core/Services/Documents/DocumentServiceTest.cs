using Digital.Net.Core;
using Digital.Net.Core.Entities.Context;
using Digital.Net.Core.Services.Documents;
using Digital.Net.Core.Services.Documents.Exceptions;
using Digital.Net.Lib.Files;
using Digital.Net.Tests.Core.Factories.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;

namespace Digital.Net.Tests.Core.Services.Documents;

public class DocumentServiceTest : DbServiceTest<DigitalContext>
{
    private DocumentService _service = null!;

    protected override Task OnInitializedAsync()
    {
        var configMock = new Mock<IConfiguration>();
        _service = new DocumentService(Context, configMock.Object);
        return Task.CompletedTask;
    }

    [Test]
    public async Task RemoveDocumentAsync_Should_Return_Error_When_Document_Is_Not_Found()
    {
        var docId = Guid.NewGuid();
        var result = await _service.RemoveDocumentAsync(docId);
        await Assert.That(result.HasErrorOfType<DocumentNotFoundException>()).IsTrue();
    }

    [Test]
    public async Task SaveDocumentAsync_Should_Return_Error_When_Uploader_Is_Null()
    {
        var result = await _service.SaveDocumentAsync(
            Stream.Null,
            new FileDefinition { FileName = "test.txt", MimeType = "text/plain" },
            null);
        await Assert.That(result.HasErrorOfType<NoUploaderException>()).IsTrue();
    }

    [Test]
    public async Task SaveDocumentAsync_Should_Populate_Width_And_Height_For_Image()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"digital_test_{Guid.NewGuid():N}");
        var service = BuildServiceWithStorage(tempDir);

        try
        {
            var user = Context.BuildTestUser();
            var (bytes, fileName, contentType) = TestMediaDataFactory.GenerateTestImage(120, 80);
            using var stream = new MemoryStream(bytes);
            var definition = new FileDefinition { FileName = fileName, MimeType = contentType, FileSize = bytes.Length };

            var result = await service.SaveDocumentAsync(stream, definition, user);

            await Assert.That(result.HasError).IsFalse();
            await Assert.That(result.Value).IsNotNull();

            var saved = await Context.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == result.Value!.Id);
            await Assert.That(saved).IsNotNull();
            await Assert.That(saved!.Width).IsEqualTo(120);
            await Assert.That(saved.Height).IsEqualTo(80);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task SaveDocumentAsync_Should_Leave_Width_And_Height_Null_For_Non_Image()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"digital_test_{Guid.NewGuid():N}");
        var service = BuildServiceWithStorage(tempDir);

        try
        {
            var user = Context.BuildTestUser();
            var bytes = "not an image"u8.ToArray();
            using var stream = new MemoryStream(bytes);
            var definition = new FileDefinition { FileName = "test.txt", MimeType = "text/plain", FileSize = bytes.Length };

            var result = await service.SaveDocumentAsync(stream, definition, user);

            await Assert.That(result.HasError).IsFalse();
            await Assert.That(result.Value).IsNotNull();

            var saved = await Context.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == result.Value!.Id);
            await Assert.That(saved).IsNotNull();
            await Assert.That(saved!.Width).IsNull();
            await Assert.That(saved.Height).IsNull();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    private DocumentService BuildServiceWithStorage(string storagePath)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [CoreSettings.FileSystemPathKey] = storagePath
            })
            .Build();
        return new DocumentService(Context, config);
    }
}
