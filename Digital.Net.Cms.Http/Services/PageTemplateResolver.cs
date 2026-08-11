using Digital.Net.Cms.Context;
using Digital.Net.Cms.Models.Pages;
using Microsoft.EntityFrameworkCore;

namespace Digital.Net.Cms.Http.Services;

public class PageTemplateResolver(CmsContext context)
{
    /// <summary>
    ///     Resolves the published dynamic page whose pattern covers the given path. The most specific
    ///     pattern wins: fewest dynamic slugs, then longest path. Dynamic paths never inherit (no chains).
    /// </summary>
    public async Task<Page?> ResolveAsync(string? path, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path) || PagePathAnalyzer.HasDynamicSlug(path))
            return null;

        // Published only, deliberately not PageVisibility.IsLive: a template is inherited from, never
        // served, so holding one back by its date would strip the SEO off every page it covers rather
        // than schedule anything.
        var candidates = await context.Pages
            .AsNoTracking()
            .Where(p => p.Published && p.Path.Contains(":"))
            .ToListAsync(ct);

        return candidates
            .Where(p => PagePathAnalyzer.IsPatternMatch(p.Path, path))
            .OrderBy(p => PagePathAnalyzer.CountDynamicSlugs(p.Path))
            .ThenByDescending(p => p.Path.Length)
            .ThenBy(p => p.Path, StringComparer.Ordinal)
            .FirstOrDefault();
    }
}
