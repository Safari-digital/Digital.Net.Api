using System.Linq.Expressions;
using System.Text.Json;
using Digital.Net.Cms.Context;
using Digital.Net.Cms.Http.Dto;
using Digital.Net.Cms.Http.Services;
using Digital.Net.Cms.Models;
using Digital.Net.Cms.Models.Pages;
using Digital.Net.Core.Http.Security;
using Digital.Net.Core.Http.Services.Authentication.Filters;
using Digital.Net.Core.Http.Services.Crud;
using Digital.Net.Core.Http.Services.Pagination.Extensions;
using Digital.Net.Lib.Accessors;
using Digital.Net.Lib.Entities.Exceptions;
using Digital.Net.Lib.Exceptions.types;
using Digital.Net.Lib.Messages;
using Digital.Net.Lib.Predicates;
using Digital.Net.Lib.Templating;
using Digital.Net.Lib.Templating.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Digital.Net.Cms.Http.Endpoints;

public static class PageEndpoints
{
    public static IEndpointRouteBuilder MapCmsPageEndpoints(this IEndpointRouteBuilder app)
    {
        var controller = app
            .MapGroup("cms/pages")
            .WithTags("CMS.Pages")
            .RequireRateLimiting(RateLimiter.Policy)
            .RequireAuthentication(AuthorizeType.Session | AuthorizeType.ApiKey);

        controller
            .MapGet("path/availability", GetPathAvailability)
            .WithSummary("CheckPathAvailability")
            .WithDescription(
                "Returns true if the path is not yet used by any page. " +
                "ExcludeId scopes out a specific id (edition case)."
            );

        controller
            .MapGet("template", GetPageTemplate)
            .WithSummary("GetPageTemplate")
            .WithDescription(
                "Resolves the published dynamic page whose pattern covers the given path — the template " +
                "the page at this path inherits from. Returns a null value when no template applies."
            );

        controller.MapCrudSchema<CmsContext, Page>();
        controller
            .MapGet("open-graph-values/schema", GetOpenGraphSchema)
            .WithSummary("GetOpenGraphValuesSchema")
            .WithDescription("Returns the list of valid OpenGraph property keys.");

        controller
            .MapGet("template-variables", GetTemplateVariables)
            .WithSummary("GetTemplateVariables")
            .WithDescription(
                "Lists template placeholders ({{ source.field }}) exposed by every declared source. " +
                "Returns an empty list when no source exposes a [TemplateSource] field."
            );

        controller.MapCrudGet<CmsContext, Page, PageDto>();
        controller.MapPaginationGet<CmsContext, Page, PageListDto, PageQuery>(filter: PaginationFilter);
        controller
            .MapDelete("{id:guid}", DeletePage)
            .WithSummary("Delete")
            .WithDescription(
                "Deletes a page and the sheets it owns. A sheet still attached to another page — one " +
                "inherited from a template — is detached rather than deleted."
            );
        controller
            .MapPatch("{id:guid}", UpdatePage)
            .WithSummary("Patch")
            .WithDescription(
                "Applies a JSON Patch to an entity identified by its ID. Returns the patched entity as the " +
                "specified DTO type. Use the *Schema* endpoint to get the available fields."
            );
        controller
            .MapPost("", CreatePage)
            .WithSummary("Create")
            .WithDescription(
                "Creates a new entity with the provided payload. Returns the created entity as the specified DTO type."
            );

        controller
            .MapGet("{id:guid}/sheets", GetPageSheets)
            .WithSummary("GetPageSheets")
            .WithDescription("Retrieves every sheet owned by the page, ordered by load order.");

        controller
            .MapGet("{id:guid}/open-graph", GetPageOpenGraph)
            .WithSummary("GetPageOpenGraph")
            .WithDescription("Retrieves every OpenGraph entry owned by the page, ordered by index.");

        return app;
    }

    private static async Task<Ok<Result<PageDto?>>> GetPageTemplate(
        [FromQuery]
        string path,
        PageTemplateResolver templateResolver,
        CancellationToken ct
    )
    {
        var template = await templateResolver.ResolveAsync(path, ct);
        var dto = template is null
            ? null
            : new PageDto
            {
                Id = template.Id,
                Path = template.Path,
                Published = template.Published,
                Indexed = template.Indexed,
                Title = template.Title,
                Description = template.Description,
                JsonLd = template.JsonLd,
                Redirect = template.Redirect,
                CreatedAt = template.CreatedAt,
                UpdatedAt = template.UpdatedAt
            };
        return TypedResults.Ok(new Result<PageDto?>(dto));
    }

    private static async Task<Ok<Result<bool>>> GetPathAvailability(
        [FromQuery]
        string path,
        [FromQuery]
        Guid? excludeId,
        CmsContext context
    )
    {
        var taken = await context.Pages.AnyAsync(p => p.Path == path && (excludeId == null || p.Id != excludeId));
        return TypedResults.Ok(new Result<bool>(!taken));
    }

    private static async
        Task<Results<Ok<Result<List<PageSheetDto>>>, NotFound<Result<List<PageSheetDto>>>>>
        GetPageSheets(
            Guid id,
            CrudService<CmsContext, Page> crudService,
            CancellationToken ct
        )
    {
        var result = await crudService.GetChildren<Sheet, PageSheet, PageSheetDto>(id, ct);
        return result.HasErrorOfType<ResourceNotFoundException>()
            ? TypedResults.NotFound(result)
            : TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<Result<List<OpenGraphEntryDto>>>, NotFound<Result<List<OpenGraphEntryDto>>>>>
        GetPageOpenGraph(
            Guid id,
            CrudService<CmsContext, Page> crudService,
            CancellationToken ct
        )
    {
        var result = await crudService.GetChildren<OpenGraphEntry, PageOpenGraph, OpenGraphEntryDto>(id, ct);
        return result.HasErrorOfType<ResourceNotFoundException>()
            ? TypedResults.NotFound(result)
            : TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<Result<Guid>>, BadRequest<Result<Guid>>, InternalServerError<Result<Guid>>>>
        CreatePage(
            [FromBody]
            PagePayload payload,
            PageCrudService pageCrudService,
            IUserAccessor userContextService
        )
    {
        var result = await pageCrudService.CreatePage(payload, userContextService.GetUserId());
        if (result.HasErrorOfType<EntityValidationException>())
            return TypedResults.BadRequest(result);
        if (result.HasError)
            return TypedResults.InternalServerError(result);

        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<Result>, BadRequest<Result>, NotFound<Result>, InternalServerError<Result>>>
        UpdatePage(
            Guid id,
            [FromBody]
            JsonElement patch,
            PageCrudService pageCrudService,
            IUserAccessor userContextService
        )
    {
        var result = await pageCrudService.PatchPage(patch, id, userContextService.GetUserId());
        if (result.HasErrorOfType<ResourceNotFoundException>())
            return TypedResults.NotFound(result);
        if (result.HasErrorOfType<EntityValidationException>())
            return TypedResults.BadRequest(result);
        if (result.HasError)
            return TypedResults.InternalServerError(result);

        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<Result>, NotFound<Result>, InternalServerError<Result>>>
        DeletePage(
            Guid id,
            PageCrudService pageCrudService,
            CancellationToken ct
        )
    {
        var result = await pageCrudService.DeletePage(id, ct);
        if (result.HasErrorOfType<ResourceNotFoundException>())
            return TypedResults.NotFound(result);
        if (result.HasError)
            return TypedResults.InternalServerError(result);

        return TypedResults.Ok(result);
    }

    private static Ok<Result<IReadOnlyList<OpenGraphPropertySchema>>> GetOpenGraphSchema() =>
        TypedResults.Ok(new Result<IReadOnlyList<OpenGraphPropertySchema>>(OpenGraphProperties.Schema));

    private static Ok<Result<IReadOnlyList<TemplateVariableDescriptor>>> GetTemplateVariables(
        [FromServices]
        IEnumerable<TemplateSourceDescriptor> sources
    )
    {
        // Derived from the attributes by reflection, so frozen for the lifetime of the process.
        IReadOnlyList<TemplateVariableDescriptor> variables = sources
            .SelectMany(source => TemplateInterpolator.GetVariables(source.SourceType))
            .ToList();

        return TypedResults.Ok(new Result<IReadOnlyList<TemplateVariableDescriptor>>(variables));
    }

    private static Expression<Func<Page, bool>> PaginationFilter(
        Expression<Func<Page, bool>> predicate,
        PageQuery query
    )
    {
        if (!string.IsNullOrEmpty(query.Path))
            predicate = predicate.And(x => x.Path.StartsWith(query.Path));
        if (query.Published.HasValue)
            predicate = predicate.And(x => x.Published == query.Published);
        if (query.Indexed.HasValue)
            predicate = predicate.And(x => x.Indexed == query.Indexed);
        return predicate;
    }
}