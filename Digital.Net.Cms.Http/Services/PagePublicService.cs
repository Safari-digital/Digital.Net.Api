using Digital.Net.Cms.Context;
using Digital.Net.Cms.Http.Dto;
using Digital.Net.Cms.Http.Exceptions;
using Digital.Net.Cms.Models;
using Digital.Net.Cms.Models.Pages;
using Digital.Net.Lib.Entities.Templating;
using Digital.Net.Lib.Exceptions.types;
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
    public async Task<Result<List<PageSheetInfoDto>>> GetPageSheetInfos(Guid id)
    {
        var result = new Result<List<PageSheetInfoDto>>();
        try
        {
            var page = await context.Pages
                           .AsNoTracking()
                           .FirstOrDefaultAsync(p => p.Id == id)
                       ?? throw new ResourceNotFoundException();

            var own = await GetPublishedSheetInfosAsync(page.Id);
            var template = await templateResolver.ResolveAsync(page.Path);
            if (template is null)
            {
                result.Value = own;
                return result;
            }

            var ownIds = own.Select(s => s.Id).ToHashSet();
            var inherited = await GetPublishedSheetInfosAsync(template.Id);
            result.Value = inherited.Where(s => !ownIds.Contains(s.Id)).Concat(own).ToList();
        }
        catch (Exception ex)
        {
            result.AddError(ex);
        }

        return result;
    }

    /// <summary>
    ///     Builds every sheet of a page in one pass: inheritance applied, published only, ordered as the
    ///     pivot declares, and hydrated from the same source as the page itself. A page whose source is
    ///     unresolved renders its sheets un-hydrated rather than failing, exactly as its metas do.
    /// </summary>
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

            // Inherited sheets load first, then the page's own — same order as GetPageSheetInfos. A sheet
            // shared by both pages is kept once, on the dedicated page's side.
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

    public async Task<Result<(string contentType, string content)>> BuildPublicPageSheetResource(
        PageSheetBuildPayload payload,
        CancellationToken ct = default
    )
    {
        var result = new Result<(string contentType, string content)>();
        try
        {
            var (page, template, sources) = await ResolvePageAndSourcesAsync(payload, ct);
            var pageIds = template is null ? new[] { page.Id } : new[] { page.Id, template.Id };
            var pageSheet = await context.PageSheets
                .AsNoTracking()
                .Include(ps => ps.Child)
                .FirstOrDefaultAsync(
                    ps => pageIds.Contains(ps.ParentId)
                          && ps.ChildId == payload.SheetId
                          && ps.Child.Published,
                    ct
                ) ?? throw new InvalidPagePathException();

            var sheet = pageSheet.Child;
            if (sources is not null)
                TemplateInterpolator.Interpolate(sheet, sources);

            var contentType = sheet.Type switch
            {
                "css" => "text/css",
                "js" => "application/javascript",
                "html" => "text/html",
                _ => "text/plain"
            };

            result.Value = (contentType, sheet.Content);
        }
        catch (Exception ex)
        {
            result.AddError(ex);
        }

        return result;
    }

    /// <summary>
    ///     Resolves the published page whose Path is exactly the requested one, or 404. When that page
    ///     is covered by a template, the most specific one is returned alongside for merging — templates
    ///     are inherited from, never served.
    /// </summary>
    private async Task<(Page page, Page? template, IReadOnlyDictionary<string, object>? sources)>
        ResolvePageAndSourcesAsync(
            PageBuildPayload payload,
            CancellationToken ct
        )
    {
        if (string.IsNullOrWhiteSpace(payload.Path))
            throw new InvalidPagePathException();

        var dedicated = await context.Pages
            .AsNoTracking()
            .Where(PageVisibility.IsLive(DateTime.UtcNow))
            .Where(p => p.Path == payload.Path)
            .FirstOrDefaultAsync(ct);

        // A dynamic path is a pattern, not an address: a template is only ever an inheritance source,
        // never a page in its own right. Serving it for a path it merely covers would answer 200 for
        // any URL under the pattern — an unpublished article, or a slug that never existed — with its
        // own tokens as content, since no source hosts on it. Nothing to serve means 404.
        var page = dedicated ?? throw new InvalidPagePathException();
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

    private Task<List<PageSheetInfoDto>> GetPublishedSheetInfosAsync(Guid pageId) =>
        context.PageSheets
            .AsNoTracking()
            .Include(ps => ps.Child)
            .Where(ps => ps.ParentId == pageId && ps.Child.Published == true)
            .OrderBy(ps => ps.Order)
            .Select(ps => new PageSheetInfoDto
            {
                Id = ps.ChildId,
                Name = ps.Child.Name,
                Type = ps.Child.Type
            })
            .ToListAsync();

    private Task<List<OpenGraphEntry>> GetOpenGraphEntriesAsync(Guid pageId, CancellationToken ct) =>
        context.PageOpenGraphs
            .AsNoTracking()
            .Include(po => po.Child)
            .Where(po => po.ParentId == pageId)
            .OrderBy(po => po.Order)
            .Select(po => po.Child)
            .ToListAsync(ct);
}
