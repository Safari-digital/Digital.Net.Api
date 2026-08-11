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
    ///     goes with it rather than lingering as rows nothing renders.
    ///     <para>
    ///         A sheet still attached to another page survives. Inheritance is the reason — a sheet hung
    ///         on a template is rendered by every page under it, and deleting one of those pages must not
    ///         empty the others.
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

            var shared = await context.PageSheets
                .AsNoTracking()
                .Where(ps => ps.ParentId != pageId && owned.Contains(ps.ChildId))
                .Select(ps => ps.ChildId)
                .ToListAsync(ct);

            result = await crudService.Delete(pageId);
            if (result.HasError) return result;

            var doomed = owned.Except(shared).ToList();
            if (doomed.Count == 0) return result;

            var sheets = await context.Sheets.Where(s => doomed.Contains(s.Id)).ToListAsync(ct);
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
