using Digital.Net.Cms.Context;
using Digital.Net.Cms.Http.Dto;
using Digital.Net.Cms.Models.Pages;
using Digital.Net.Cms.Templating;
using Digital.Net.Lib.Date;
using Microsoft.EntityFrameworkCore;

namespace Digital.Net.Cms.Http.Services;

public class SitemapService(CmsContext context)
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
            // A pattern is a template, not a page: it has nothing of its own to list.
        }

        return entries;
    }

}
