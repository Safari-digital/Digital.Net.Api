using System.Text.Json;
using Digital.Net.Cms.Context;
using Digital.Net.Cms.Http.Dto;
using Digital.Net.Cms.Models.Pages;
using Digital.Net.Core.Http.Services.Crud;
using Digital.Net.Lib.Messages;
using Microsoft.EntityFrameworkCore;

namespace Digital.Net.Cms.Http.Services;

public class PageCrudService(
    CmsContext context,
    CrudService<CmsContext, Page> crudService
)
{
    /// <summary>
    ///     Deletes a page and the sheets it owns: a page is what holds content together, so its content
    ///     goes with it rather than lingering as rows nothing renders. Only the pivot cascades at the
    ///     database level, which is what used to leave them behind.
    ///     <para>
    ///         Every sheet of the page goes, with no exception to make: <see cref="PageSheet" /> is
    ///         declared <see cref="Ownership.Cascade" />, and in that mode the pivot resolver never links
    ///         an existing sheet to a second page — it always creates a new one. A sheet therefore belongs
    ///         to exactly one page.
    ///     </para>
    ///     <para>
    ///         Inheritance does not change that: a page inheriting from a template holds no pivot row
    ///         towards the template's sheets, the union being computed at read time. Deleting an
    ///         inheriting page cannot reach them — and deleting the template does take its sheets away
    ///         from every page that was inheriting them.
    ///     </para>
    ///     <para>
    ///         The sheets go through the tracker rather than ExecuteDelete, so their mutations are audited
    ///         and the caches keyed on them are invalidated.
    ///     </para>
    /// </summary>
    public async Task<Result> DeletePage(Guid pageId, CancellationToken ct = default)
    {
        var result = new Result();
        try
        {
            var owned = await context.PageSheets
                .AsNoTracking()
                .Where(ps => ps.ParentId == pageId)
                .Select(ps => ps.ChildId)
                .ToListAsync(ct);

            result = await crudService.Delete(pageId);
            if (result.HasError || owned.Count == 0) return result;

            var sheets = await context.Sheets.Where(s => owned.Contains(s.Id)).ToListAsync(ct);
            context.Sheets.RemoveRange(sheets);
            await context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            result.AddError(ex);
        }

        return result;
    }

    public async Task<Result<Guid>> CreatePage(PagePayload payload, Guid userId)
    {
        var result = new Result<Guid>();
        try
        {
            result = await crudService.Create(new Page { Path = payload.Path });
        }
        catch (Exception ex)
        {
            result.AddError(ex);
        }

        return result;
    }

    public async Task<Result> PatchPage(JsonElement patch, Guid pageId, Guid userId, CancellationToken ct = default)
    {
        var result = new Result();
        try
        {
            result = await crudService.Patch(patch, pageId, ct);
        }
        catch (Exception ex)
        {
            result.AddError(ex);
        }

        return result;
    }
}
