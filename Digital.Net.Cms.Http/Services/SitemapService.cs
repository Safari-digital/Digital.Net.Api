using Digital.Net.Cms.Context;
using Digital.Net.Cms.Http.Dto;
using Digital.Net.Cms.Models.Pages;
using Digital.Net.Cms.Templating;
using Digital.Net.Lib.Date;
using Microsoft.EntityFrameworkCore;

namespace Digital.Net.Cms.Http.Services;

public class SitemapService(CmsContext context)
{
    /// <summary>
    ///     Every published, indexed page that has a real address. A path carrying a dynamic slug is a
    ///     template — a pattern, not a page — so it has nothing of its own to list; the pages it covers
    ///     each own their path and appear here on their own account.
    /// </summary>
    public async Task<List<SitemapEntryDto>> GetEntriesAsync() =>
        await context.Pages
            .AsNoTracking()
            .Where(p => p.Published && p.Indexed && !p.Path.Contains(":"))
            .Select(p => new SitemapEntryDto { Path = p.Path, UpdatedAt = p.UpdatedAt })
            .ToListAsync();
}
