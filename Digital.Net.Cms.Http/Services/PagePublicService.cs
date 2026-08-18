using Digital.Net.Cms.Context;
using Digital.Net.Cms.Http.Dto;
using Digital.Net.Cms.Http.Exceptions;
using Digital.Net.Cms.Models;
using Digital.Net.Cms.Models.Pages;
using Digital.Net.Lib.Entities.Templating;
using Digital.Net.Lib.Messages;
using Digital.Net.Lib.Templating;
using Microsoft.EntityFrameworkCore;

namespace Digital.Net.Cms.Http.Services;

public class PagePublicService(
    CmsContext context,
    PageTemplateResolver templateResolver,
    TemplatingService templatingService
)
{
    public async Task<Result<List<PageSheetResourceDto>>> BuildPublicPageSheets(
        PageBuildPayload payload,
        CancellationToken ct = default
    )
    {
        var result = new Result<List<PageSheetResourceDto>>();
        try
        {
            var (page, template, sources) = await ResolvePageAndSourcesAsync(payload, ct);
            var pageIds = template is null ? new[] { page.Id } : [page.Id, template.Id];

            var pageSheets = await context.PageSheets
                .AsNoTracking()
                .Include(ps => ps.Child)
                .Where(ps => pageIds.Contains(ps.ParentId) && ps.Child.Published)
                .ToListAsync(ct);

            // Inherited sheets load first, then the page's own. A sheet shared by both pages is kept
            // once, on the dedicated page's side.
            var ownIds = pageSheets.Where(ps => ps.ParentId == page.Id).Select(ps => ps.ChildId).ToHashSet();
            var ordered = pageSheets
                .Where(ps => ps.ParentId != page.Id && !ownIds.Contains(ps.ChildId))
                .OrderBy(ps => ps.Order)
                .Concat(pageSheets.Where(ps => ps.ParentId == page.Id).OrderBy(ps => ps.Order))
                .ToList();

            result.Value = ordered
                .Select(ps =>
                {
                    var sheet = ps.Child;
                    if (sources is not null)
                        TemplateInterpolator.Interpolate(sheet, sources);

                    return new PageSheetResourceDto
                    {
                        Id = sheet.Id,
                        Name = sheet.Name,
                        Type = sheet.Type,
                        Content = sheet.Content
                    };
                })
                .ToList();
        }
        catch (Exception ex)
        {
            result.AddError(ex);
        }

        return result;
    }

    public async Task<Result<PagePublicDto>> BuildPublicPage(PageBuildPayload payload, CancellationToken ct = default)
    {
        var result = new Result<PagePublicDto>();
        try
        {
            var (page, template, sources) = await ResolvePageAndSourcesAsync(payload, ct);

            var openGraph = await GetOpenGraphEntriesAsync(page.Id, ct);
            if (template is not null)
            {
                var overridden = openGraph.Select(e => e.Property).ToHashSet();
                var inherited = await GetOpenGraphEntriesAsync(template.Id, ct);
                openGraph = inherited.Where(e => !overridden.Contains(e.Property)).Concat(openGraph).ToList();
            }

            if (sources is not null)
            {
                TemplateInterpolator.Interpolate(page, sources);
                foreach (var entry in openGraph)
                    TemplateInterpolator.Interpolate(entry, sources);
            }

            result.Value = new PagePublicDto(page)
            {
                // An entry left empty by its own chain is dropped rather than served blank. That is what
                // makes an interpolated entry optional: a template can offer og:image to every page it
                // covers, and the pages whose source has none simply do not carry the tag.
                OpenGraph = openGraph
                    .Where(e => !string.IsNullOrWhiteSpace(e.Content))
                    .Select(e => new OpenGraphEntryPublicDto { Property = e.Property, Content = e.Content })
                    .ToList()
            };
        }
        catch (Exception ex)
        {
            result.AddError(ex);
        }

        return result;
    }

    private async Task<(Page page, Page? template, IReadOnlyDictionary<string, object>? sources)>
        ResolvePageAndSourcesAsync(
            PageBuildPayload payload,
            CancellationToken ct
        )
    {
        if (string.IsNullOrWhiteSpace(payload.Path))
            throw new InvalidPagePathException();

        var page = await context.Pages
            .AsNoTracking()
            .Where(PageExpressions.IsLive())
            .Where(p => p.Path == payload.Path)
            .FirstOrDefaultAsync(ct) ?? throw new InvalidPagePathException();

        var template = await templateResolver.ResolveAsync(payload.Path, ct);

        // Sources hang off whichever page declares the pattern: the dedicated page itself, or the
        // template it inherits from when the dedicated page is a static child. The dedicated page wins,
        // so a page carrying its own source is never fed by the one its template offers.
        var source = await templatingService.ResolveSourceAsync(page.Id, ct)
                     ?? (template is null ? null : await templatingService.ResolveSourceAsync(template.Id, ct));

        IReadOnlyDictionary<string, object>? sources = source is null
            ? null
            : new Dictionary<string, object>
            {
                [source.GetCanonicalType().Name.ToLowerInvariant()] = source
            };

        // A page with no source is not an error: a static page has none, and a page whose source is
        // gone renders its placeholders rather than disappearing. Publication is what gates a page,
        // and the query above already applied it.

        if (template is not null)
            MergeTemplateValues(page, template);

        return (page, template, sources);
    }

    private static void MergeTemplateValues(Page page, Page template)
    {
        if (string.IsNullOrWhiteSpace(page.Title))
            page.Title = template.Title;
        if (string.IsNullOrWhiteSpace(page.Description))
            page.Description = template.Description;
        if (string.IsNullOrWhiteSpace(page.JsonLd))
            page.JsonLd = template.JsonLd;
        if (string.IsNullOrWhiteSpace(page.Redirect))
            page.Redirect = template.Redirect;
    }

    private Task<List<OpenGraphEntry>> GetOpenGraphEntriesAsync(Guid pageId, CancellationToken ct) =>
        context.PageOpenGraphs
            .AsNoTracking()
            .Include(po => po.Child)
            .Where(po => po.ParentId == pageId)
            .OrderBy(po => po.Order)
            .Select(po => po.Child)
            .ToListAsync(ct);
}
