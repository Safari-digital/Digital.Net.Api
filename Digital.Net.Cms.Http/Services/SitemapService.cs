using Digital.Net.Cms.Context;
using Digital.Net.Cms.Http.Dto;
using Digital.Net.Cms.Models.Pages;
using Microsoft.EntityFrameworkCore;

namespace Digital.Net.Cms.Http.Services;

public class SitemapService(CmsContext context)
{
    public async Task<List<SitemapEntryDto>> GetEntriesAsync() =>
        await context.Pages
            .AsNoTracking()
            .Where(PageExpressions.IsLive())
            .Where(p => p.Indexed && !p.Path.Contains(":"))
            .Select(p => new SitemapEntryDto { Path = p.Path, UpdatedAt = p.UpdatedAt })
            .ToListAsync();
}
