namespace Digital.Net.Cms.Http.Dto;

public class PageBuildPayload
{
    public required string Path { get; set; }

    /// <summary>
    ///     Transitional. Tells apart the sources sharing a template page, until every source owns a
    ///     dedicated page and the contract reduces to Path alone.
    /// </summary>
    public string? PageSlug { get; set; }
}
