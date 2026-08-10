using System.Text.Json;
using Digital.Net.Cms.Context;
using Digital.Net.Cms.Http.Dto;
using Digital.Net.Cms.Models.Pages;
using Digital.Net.Core.Http.Services.Crud;
using Digital.Net.Lib.Messages;

namespace Digital.Net.Cms.Http.Services;

public class PageCrudService(
    CrudService<CmsContext, Page> crudService
)
{
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
