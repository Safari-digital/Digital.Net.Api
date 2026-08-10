namespace Digital.Net.Cms.Http.Dto;

/// <summary>
///     A sheet of a page, inheritance applied and placeholders already hydrated. Lets a client render a
///     page in one round-trip instead of one per sheet.
/// </summary>
public class PageSheetResourceDto
{
    public required Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Type { get; set; }
    public required string Content { get; set; }
}
