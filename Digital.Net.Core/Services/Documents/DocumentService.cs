using Digital.Net.Core.Entities.Context;
using Digital.Net.Core.Entities.Models.Documents;
using Digital.Net.Core.Entities.Models.Users;
using Digital.Net.Core.Services.Documents.Exceptions;
using Digital.Net.Core.Services.Documents.Utils;
using Digital.Net.Lib.Configuration;
using Digital.Net.Lib.Files;
using Digital.Net.Lib.Messages;
using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using DocumentDimensionExtractor = Digital.Net.Core.Services.Documents.Utils.DocumentDimensionExtractor;
using SvgSanitizer = Digital.Net.Core.Services.Documents.Utils.SvgSanitizer;

namespace Digital.Net.Core.Services.Documents;

public class DocumentService(
    DigitalContext context,
    IConfiguration configuration
) : IDocumentService
{
    public string GetDocumentPath(Document document) => Path.Combine(
        configuration.Get<string>(CoreSettings.FileSystemPathKey) ?? CoreSettings.DefaultFileSystemPath,
        document.FileName
    );

    public string? ResolveExistingPath(Document document)
    {
        var path = Path.GetFullPath(GetDocumentPath(document));
        return File.Exists(path) ? path : null;
    }

    public async Task<Result<Document>> SaveImageDocumentAsync(
        Stream content,
        FileDefinition definition,
        User? uploader,
        int? quality = null
    )
    {
        var result = new Result<Document>();
        MemoryStream compressed;
        string fileName;
        try
        {
            using var image = await Image.LoadAsync(content);
            compressed = new MemoryStream();
            await image.SaveAsync(compressed, FormFileHelper.GetJpegEncoder(quality));
            compressed.Position = 0;
            fileName = $"{FormFileHelper.GenerateFileName()}.jpg";
        }
        catch (Exception ex)
        {
            return result.AddError(ex);
        }

        var imageDefinition = new FileDefinition
        {
            FileName = fileName,
            MimeType = "image/jpeg",
            FileSize = compressed.Length
        };

        await using (compressed)
            return await SaveDocumentAsync(compressed, imageDefinition, uploader);
    }

    public async Task<Result> RemoveDocumentAsync(Guid id)
    {
        var document = await context.Documents.FindAsync(id);
        var result = new Result();
        if (document is null)
            return result.AddError(new DocumentNotFoundException());

        var path = GetDocumentPath(document);
        if (File.Exists(path))
            File.Delete(path);

        context.Documents.Remove(document);
        await context.SaveChangesAsync();
        return result;
    }

    public async Task<Result<Document>> SaveDocumentAsync(Stream content, FileDefinition definition, User? uploader)
    {
        var result = new Result<Document>();
        if (uploader is null)
            return result.AddError(new NoUploaderException());

        var payload = content;
        if (DocumentTypes.SvgMimeTypes.Contains(definition.MimeType, StringComparer.OrdinalIgnoreCase))
            payload = await SvgSanitizer.SanitizeAsync(content);

        // Buffer once so the bytes can be read twice (dimension extraction + disk write)
        // and to support non-seekable source streams.
        using var buffer = new MemoryStream();
        await payload.CopyToAsync(buffer);
        buffer.Position = 0;

        result.Value = new Document(uploader, definition);

        var (width, height) = DocumentDimensionExtractor.Extract(buffer, definition.MimeType);
        result.Value.Width = width;
        result.Value.Height = height;
        buffer.Position = 0;

        await context.Documents.AddAsync(result.Value);

        var fullPath = Path.GetFullPath(GetDocumentPath(result.Value));
        var dir = Path.GetDirectoryName(fullPath);
        if (dir is not null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        await using (var stream = new FileStream(fullPath, FileMode.Create))
            await buffer.CopyToAsync(stream);

        await context.SaveChangesAsync();
        return result;
    }
}
