using Digital.Net.Lib.Messages;

namespace Digital.Net.Cms.Http.Services;

/// <summary>
///     Suggests the media labels already in use, for the back-office autocomplete.
///     <para>
///         It used to read them from ArticleMedia. Article left the library, and with it the only pivot
///         that labelled a media — so the CMS has no labels of its own to offer. The endpoint keeps its
///         contract and answers empty rather than disappearing, which would break the input that calls
///         it; suggesting client labels belongs to whoever owns the labelled pivot now.
///     </para>
/// </summary>
public class MediaLabelService
{
    public Task<Result<List<string>>> GetExistingLabels(string? search, CancellationToken ct) =>
        Task.FromResult(new Result<List<string>> { Value = [] });
}
