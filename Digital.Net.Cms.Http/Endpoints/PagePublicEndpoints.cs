using Digital.Net.Cms.Http.Dto;
using Digital.Net.Cms.Http.Exceptions;
using Digital.Net.Cms.Http.Services;
using Digital.Net.Core.Http.Security;
using Digital.Net.Core.Http.Services.Authentication.Filters;
using Digital.Net.Lib.Exceptions.types;
using Digital.Net.Lib.Messages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Digital.Net.Cms.Http.Endpoints;

public static class PagePublicEndpoints
{
    public static IEndpointRouteBuilder MapCmsPagePublicEndpoints(this IEndpointRouteBuilder app)
    {
        var publicController = app
            .MapGroup("cms/pages/public")
            .WithTags("CMS.Pages.Public")
            .RequireRateLimiting(RateLimiter.Policy)
            .RequireAuthentication(AuthorizeType.Application | AuthorizeType.Session | AuthorizeType.ApiKey);

        publicController
            .MapPost("build", BuildPublicPage)
            .WithSummary("Build")
            .WithDescription(
                "Builds a published page response for the templated Path declared by the client, " +
                "interpolating [TemplateTarget] fields with the source instance hosted by the page. " +
                "Pages hosting no source are served as-is."
            );

        publicController
            .MapPost("build/sheets", BuildPublicPageSheets)
            .WithSummary("BuildSheets")
            .WithDescription(
                "Builds every published sheet of the page declared by the client, inheritance applied and " +
                "content interpolated, ordered by load order. One round-trip instead of one per sheet."
            );
        
        return app;
    }

    private static async Task<Results<
            Ok<Result<PagePublicDto>>,
            BadRequest<Result<PagePublicDto>>,
            InternalServerError<Result<PagePublicDto>>
        >>
        BuildPublicPage(
            [FromBody]
            PageBuildPayload payload,
            PagePublicService pagePublicService,
            CancellationToken ct
        )
    {
        var result = await pagePublicService.BuildPublicPage(payload, ct);
        if (result.HasErrorOfType<InvalidPagePathException>())
            return TypedResults.BadRequest(result);
        if (result.HasError)
            return TypedResults.InternalServerError(result);

        return TypedResults.Ok(result);
    }

    private static async Task<Results<
            Ok<Result<List<PageSheetResourceDto>>>,
            BadRequest<Result<List<PageSheetResourceDto>>>,
            InternalServerError<Result<List<PageSheetResourceDto>>>
        >>
        BuildPublicPageSheets(
            [FromBody]
            PageBuildPayload payload,
            PagePublicService pagePublicService,
            CancellationToken ct
        )
    {
        var result = await pagePublicService.BuildPublicPageSheets(payload, ct);
        if (result.HasErrorOfType<InvalidPagePathException>())
            return TypedResults.BadRequest(result);
        if (result.HasError)
            return TypedResults.InternalServerError(result);

        return TypedResults.Ok(result);
    }
}