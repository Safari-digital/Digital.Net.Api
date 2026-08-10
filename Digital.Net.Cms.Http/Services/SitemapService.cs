using Digital.Net.Cms.Context;
using Digital.Net.Cms.Http.Dto;
using Digital.Net.Cms.Models.Pages;
using Digital.Net.Cms.Templating;
using Digital.Net.Lib.Date;
using Microsoft.EntityFrameworkCore;

namespace Digital.Net.Cms.Http.Services;

public class SitemapService(CmsContext context, IEnumerable<ITemplateSourceResolver> sourceResolvers)
{
    public async Task<List<SitemapEntryDto>> GetEntriesAsync()
    {
        var pages = await context.Pages
            .AsNoTracking()
            .Where(p => p.Published)
            .ToListAsync();

        // A published dedicated page owns its path: it decides its own sitemap presence,
        // so template expansions never re-list it (even when it opted out via Indexed).
        var dedicatedPaths = pages
            .Where(p => !PagePathAnalyzer.HasDynamicSlug(p.Path))
            .Select(p => p.Path)
            .ToHashSet();

        var entries = new List<SitemapEntryDto>();
        var seen = new HashSet<string>();
        foreach (var page in pages.Where(p => p.Indexed))
        {
            if (!PagePathAnalyzer.HasDynamicSlug(page.Path))
            {
                if (seen.Add(page.Path))
                    entries.Add(new SitemapEntryDto { Path = page.Path, UpdatedAt = page.UpdatedAt });
                continue;
            }
            entries.AddRange((await ResolveDynamicPageAsync(page))
                .Where(e => !dedicatedPaths.Contains(e.Path) && seen.Add(e.Path)));
        }

        return entries;
    }

    /// <summary>
    ///     Unfolds a pattern into one entry per source attached to it. Disappears once every source owns
    ///     its dedicated page, which the loop above already lists on its own.
    /// </summary>
    private async Task<List<SitemapEntryDto>> ResolveDynamicPageAsync(Page page)
    {
        var entries = new List<SitemapEntryDto>();
        foreach (var resolver in sourceResolvers)
        foreach (var source in await resolver.ListForPageAsync(page.Id))
        {
            // Without a discriminator there is no value to substitute into the pattern.
            var discriminator = resolver.GetDiscriminatorValue(source);
            if (string.IsNullOrEmpty(discriminator))
                continue;

            entries.Add(new SitemapEntryDto
            {
                Path = PagePathAnalyzer.ResolveDynamicPath(page.Path, discriminator),
                UpdatedAt = DateTimeResolver.MaxUpdatedAt(source.UpdatedAt, page.UpdatedAt)
            });
        }

        return entries;
    }
}
