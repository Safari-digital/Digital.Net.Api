using Digital.Net.Cms.Context;
using Digital.Net.Cms.Http.Dto;
using Digital.Net.Cms.Http.Exceptions;
using Digital.Net.Cms.Models;
using Digital.Net.Cms.Models.Pages;
using Digital.Net.Cms.Templating;
using Digital.Net.Lib.Entities.Models;
using Digital.Net.Core.Services.Templating;
using Digital.Net.Lib.Exceptions.types;
using Digital.Net.Lib.Messages;
using Microsoft.EntityFrameworkCore;

namespace Digital.Net.Cms.Http.Services;

public class PagePublicService(
    CmsContext context,
    PageTemplateResolver templateResolver,
    IEnumerable<ITemplateSourceResolver> sourceResolvers
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
                        TemplateInterpolator.HydrateInPlace(sheet, sources);

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
                TemplateInterpolator.HydrateInPlace(page, sources);
                foreach (var entry in openGraph)
                    TemplateInterpolator.HydrateInPlace(entry, sources);
            }

            result.Value = new PagePublicDto(page)
            {
                OpenGraph = openGraph
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
                TemplateInterpolator.HydrateInPlace(sheet, sources);

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
    ///     Three-step resolution: the published page whose Path is exactly the requested one, otherwise
    ///     the most specific published template covering it, otherwise 404. When a dedicated page is
    ///     covered by a template, the template is returned alongside for merging.
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
            .Where(p => p.Path == payload.Path && p.Published)
            .FirstOrDefaultAsync(ct);

        var template = await templateResolver.ResolveAsync(payload.Path, ct);
        var page = dedicated ?? template ?? throw new InvalidPagePathException();
        if (dedicated is null)
            template = null;

        IReadOnlyDictionary<string, object>? sources = null;
        if (!string.IsNullOrEmpty(payload.PageSlug))
        {
            // Sources hang off whichever page declares the pattern: the dedicated page itself, or the
            // template it inherits from when the dedicated page is a static child.
            var owners = template is null ? new[] { page.Id } : [page.Id, template.Id];
            var source = await ResolveSourceAsync(owners, payload.PageSlug, ct);
            // A static dedicated page stands on its own: a missing interpolation source only
            // 404s when a template answers (unknown article slugs must keep returning 404).
            var servesDedicatedPage = dedicated is not null && !PagePathAnalyzer.HasDynamicSlug(dedicated.Path);
            if (source is null && !servesDedicatedPage)
                throw new InvalidPagePathException();
            if (source is not null)
                sources = new Dictionary<string, object>
                {
                    [source.GetCanonicalType().Name.ToLowerInvariant()] = source
                };
        }

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

    /// <summary>
    ///     Queries the declared sources until one answers for the owning pages. As many round-trips as
    ///     declared sources, which is without stake at this scale.
    /// </summary>
    private async Task<Entity?> ResolveSourceAsync(
        IReadOnlyList<Guid> owners,
        string slug,
        CancellationToken ct = default
    )
    {
        foreach (var resolver in sourceResolvers)
        {
            var source = await resolver.ResolveAsync(owners, slug, ct);
            if (source is not null)
                return source;
        }

        return null;
    }
}
