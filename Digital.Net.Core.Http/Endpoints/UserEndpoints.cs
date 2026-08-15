using System.Linq.Expressions;
using Digital.Net.Core.Entities.Context;
using Digital.Net.Core.Entities.Models.Auth;
using Digital.Net.Core.Entities.Models.Users;
using Digital.Net.Core.Http.Accessors;
using Digital.Net.Core.Http.Endpoints.Dto;
using Digital.Net.Core.Http.Security;
using Digital.Net.Core.Http.Services.Authentication;
using Digital.Net.Core.Http.Services.Authentication.Accessor;
using Digital.Net.Core.Http.Services.Authentication.Filters;
using Digital.Net.Core.Http.Services.Authentication.Options;
using Digital.Net.Core.Http.Services.Crud;
using Digital.Net.Core.Http.Services.Documents;
using Digital.Net.Core.Http.Services.Pagination.Extensions;
using Digital.Net.Core.Services.Documents.Exceptions;
using Digital.Net.Core.Services.Users;
using Digital.Net.Core.Services.Users.Exceptions;
using Digital.Net.Lib.Entities;
using Digital.Net.Lib.Exceptions.types;
using Digital.Net.Lib.Files;
using Digital.Net.Lib.Messages;
using Digital.Net.Lib.Predicates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Digital.Net.Core.Http.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var controller = app
            .MapGroup("/user")
            .WithTags("User")
            .RequireRateLimiting(RateLimiter.Policy)
            .RequireAuthentication(AuthorizeType.Session | AuthorizeType.ApiKey);

        controller.MapCrudSchema<DigitalContext, User>("");

        controller
            .MapCrudGet<DigitalContext, User, UserDto>("")
            .WithSummary("GetUserById")
            .WithDescription("Retrieves a user by their unique identifier.")
            .RequireAdmin();

        controller
            .MapPaginationGet<DigitalContext, User, UserListDto, UserQuery>("", PaginationFilter)
            .WithSummary("GetPaginatedUsers")
            .WithDescription("Retrieves a paginated list of users with filtering and sorting options.")
            .RequireAdmin();

        controller
            .MapPost("", CreateUser)
            .WithSummary("CreateUser")
            .WithDescription("Creates a new user with the provided information.")
            .RequireAdmin();

        controller
            .MapDelete("/{id:guid}", DeleteUser)
            .WithSummary("DeleteUser")
            .WithDescription("Deletes a user after admin password confirmation. Admin users cannot be deleted.")
            .RequireAdmin();

        controller
            .MapPut("/{id:guid}/status", UpdateUserStatus)
            .WithSummary("UpdateUserStatus")
            .WithDescription("Activates or deactivates a user. Admin users cannot be deactivated.")
            .RequireAdmin();

        controller
            .MapPut("/{id:guid}/role", UpdateUserRole)
            .WithSummary("UpdateUserRole")
            .WithDescription("Grants or revokes admin privileges. Existing admins cannot be demoted.")
            .RequireAdmin();

        controller
            .MapPaginationGet<DigitalContext, AuthEvent, AuthEventDto, AuthEventQuery>("auth-events", PaginationFilter)
            .WithSummary("GetPaginatedAuthEvents")
            .WithDescription(
                "Retrieves the paginated audit log of authentication events " +
                "(login/logout/password changes), newest first by default."
            )
            .RequireAdmin();

        controller
            .MapGet("/{id:guid}/avatar", GetUserAvatar)
            .WithSummary("GetUserAvatar")
            .WithDescription("Retrieves the avatar image file for a given user.");

        controller
            .MapGet("/self", GetSelf)
            .WithSummary("GetSelf")
            .WithDescription("Retrieves the authenticated user's information.");

        controller
            .MapPut("/self/password", UpdatePassword)
            .WithSummary("UpdatePassword")
            .WithDescription(
                "Updates the authenticated user's password. A password change revokes the account's API keys "
                + "and all its sessions; the caller gets a fresh session cookie."
            );

        controller
            .MapPut("/self/avatar", UpdateAvatar)
            .DisableAntiforgery() // No ASP.NET antiforgery pipeline here: would throw if enabled
            .WithSummary("UpdateAvatar")
            .WithDescription("Updates the authenticated user's avatar.");

        controller
            .MapDelete("/self/avatar", RemoveAvatar)
            .WithSummary("RemoveAvatar")
            .WithDescription("Removes the authenticated user's avatar.");

        controller
            .MapGet("/self/is-admin", GetSelfIsAdmin)
            .WithSummary("GetSelfIsAdmin")
            .WithDescription("Returns whether the authenticated user has admin privileges.");

        return app;
    }

    private static async Task<Results<Ok<Result<bool>>, UnauthorizedHttpResult>> GetSelfIsAdmin(
        IAuthorizedUserAccessor userContextService,
        CancellationToken ct
    )
    {
        if (await userContextService.GetUserAsync(ct) is not { } user)
            return TypedResults.Unauthorized();

        return TypedResults.Ok(new Result<bool>(user.IsAdmin));
    }

    private static async Task<Results<Ok<Result<UserDto>>, UnauthorizedHttpResult>> GetSelf(
        IAuthorizedUserAccessor userContextService,
        CancellationToken ct
    )
    {
        if (await userContextService.GetUserAsync(ct) is not { } user)
            return TypedResults.Unauthorized();

        var dto = new UserDto(user);
        return TypedResults.Ok(new Result<UserDto>(dto));
    }

    private static async
        Task<Results<Ok<Result>, BadRequest<Result>, InternalServerError<Result>, UnauthorizedHttpResult>>
        UpdatePassword(
        [FromBody]
        UserPasswordUpdatePayload request,
        UserService userService,
        AuthEventService authEventService,
        SessionService sessionService,
        AuthenticationOptionService authOptions,
        SessionCookieHandler cookieHandler,
        IAuthorizedUserAccessor userAccessor,
        HttpContext ctx
    )
    {
        var user = await userAccessor.GetUserAsync(ctx.RequestAborted);
        var result = await userService.UpdatePasswordAsync(user, request.CurrentPassword, request.NewPassword);

        await authEventService.RecordAsync(
            AuthEventType.PasswordChange,
            !result.HasError,
            ctx.GetRemoteIpAddress(),
            ctx.GetUserAgent(),
            user.Id
        );

        if (result.HasErrorOfType<PasswordMalformedException>())
            return TypedResults.BadRequest(result);
        if (result.HasErrorOfType<InvalidCredentialsException>())
            return TypedResults.Unauthorized();
        if (result.HasError)
            return TypedResults.InternalServerError(result);

        if (userAccessor.GetAuthorizationResult().Scheme == AuthorizeType.Session)
        {
            var sessionId = await sessionService.CreateAsync(
                user.Id,
                ctx.GetUserAgent() ?? string.Empty,
                ctx.RequestAborted
            );
            cookieHandler.Append(sessionId, authOptions.GetAbsoluteExpirationDate());
        }

        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok, BadRequest<Result>, InternalServerError<Result>>> UpdateAvatar(
        IFormFile avatar,
        UserService userService,
        IAuthorizedUserAccessor userContextService,
        CancellationToken ct
    )
    {
        var user = await userContextService.GetUserAsync(ct);
        await using var stream = avatar.OpenReadStream();
        var definition = new FileDefinition
        {
            FileName = avatar.FileName,
            MimeType = avatar.ContentType,
            FileSize = avatar.Length
        };
        var result = await userService.UpdateAvatar(user, stream, definition);

        if (result.HasErrorOfType<TooHeavyException>() || result.HasErrorOfType<UnsupportedFormatException>())
            return TypedResults.BadRequest(result);
        if (result.HasError)
            return TypedResults.InternalServerError(result);

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok, InternalServerError<Result>>> RemoveAvatar(
        UserService userService,
        IAuthorizedUserAccessor userContextService,
        CancellationToken ct
    )
    {
        var user = await userContextService.GetUserAsync(ct);
        var result = await userService.RemoveUserAvatar(user);

        if (result.HasError)
            return TypedResults.InternalServerError(result);

        return TypedResults.Ok();
    }

    private static async Task<IResult> GetUserAvatar(
        Guid id,
        UserService userService,
        DocumentCacheService documentCacheService
    )
    {
        var result = await userService.GetUserAvatarDocumentAsync(id);

        if (result.HasError || result.Value is null)
            return Results.NotFound();

        var cacheResult = documentCacheService.GetCachedDocumentFile(result.Value);
        if (cacheResult.HasErrorOfType<DocumentNotFoundException>())
            return Results.NotFound();
        if (cacheResult.HasError || cacheResult.Value is null)
            return Results.InternalServerError();

        return cacheResult.Value;
    }

    private static async Task<Results<Ok<Result<Guid>>, BadRequest<Result<Guid>>>> CreateUser(
        [FromBody]
        UserPayload payload,
        UserService userService
    )
    {
        var result = await userService.CreateUserAsync(
            login: payload.Login,
            username: payload.Username,
            email: payload.Email,
            password: payload.Password
        );

        return result.HasError
            ? TypedResults.BadRequest(result)
            : TypedResults.Ok(result);
    }

    private static async
        Task<Results<
            Ok<Result>,
            BadRequest<Result>,
            NotFound<Result>,
            InternalServerError<Result>,
            StatusCodeHttpResult,
            UnauthorizedHttpResult>>
        DeleteUser(
            Guid id,
            [FromBody]
            UserDeletePayload payload,
            UserService userService,
            IAuthorizedUserAccessor userContextService,
            CancellationToken ct
        )
    {
        var admin = await userContextService.GetUserAsync(ct);

        if (!UserPassword.Verify(admin, payload.Password))
            return TypedResults.Unauthorized();

        var result = await userService.DeleteUserAsync(id);

        if (result.HasErrorOfType<CannotDeleteAdminException>())
            return TypedResults.StatusCode(403);
        if (result.HasErrorOfType<ResourceNotFoundException>())
            return TypedResults.NotFound(result);
        if (result.HasError)
            return TypedResults.InternalServerError(result);

        return TypedResults.Ok(result);
    }

    private static Expression<Func<User, bool>> PaginationFilter(
        this Expression<Func<User, bool>> predicate,
        UserQuery query
    )
    {
        if (!string.IsNullOrEmpty(query.Username))
            predicate = predicate.And(x => EF.Functions.Like(x.Username, $"{EFCoreUtils.EscapeLike(query.Username)}%"));
        if (!string.IsNullOrEmpty(query.Email))
            predicate = predicate.And(x => x.Email.StartsWith(query.Email));
        if (query.IsActive.HasValue)
            predicate = predicate.And(x => x.IsActive == query.IsActive);
        if (query.IsAdmin.HasValue)
            predicate = predicate.And(x => x.IsAdmin == query.IsAdmin);
        return predicate;
    }

    private static async
        Task<Results<
            Ok<Result>,
            NotFound<Result>,
            InternalServerError<Result>,
            StatusCodeHttpResult>>
        UpdateUserStatus(
            Guid id,
            [FromBody]
            UserStatusPayload payload,
            UserService userService
        )
    {
        var result = await userService.UpdateUserStatusAsync(id, payload.IsActive);

        if (result.HasErrorOfType<CannotRevokeAdminException>())
            return TypedResults.StatusCode(403);
        if (result.HasErrorOfType<ResourceNotFoundException>())
            return TypedResults.NotFound(result);
        if (result.HasError)
            return TypedResults.InternalServerError(result);

        return TypedResults.Ok(result);
    }

    private static async
        Task<Results<
            Ok<Result>,
            NotFound<Result>,
            InternalServerError<Result>,
            StatusCodeHttpResult,
            UnauthorizedHttpResult>>
        UpdateUserRole(
            Guid id,
            [FromBody]
            UserRolePayload payload,
            UserService userService,
            IAuthorizedUserAccessor userContextService,
            CancellationToken ct
        )
    {
        var admin = await userContextService.GetUserAsync(ct);

        if (!UserPassword.Verify(admin, payload.Password))
            return TypedResults.Unauthorized();

        var result = await userService.UpdateUserRoleAsync(id, payload.IsAdmin);
        if (result.HasErrorOfType<CannotDemoteAdminException>())
            return TypedResults.StatusCode(403);
        if (result.HasErrorOfType<ResourceNotFoundException>())
            return TypedResults.NotFound(result);
        if (result.HasError)
            return TypedResults.InternalServerError(result);

        return TypedResults.Ok(result);
    }

    private static Expression<Func<AuthEvent, bool>> PaginationFilter(
        this Expression<Func<AuthEvent, bool>> predicate,
        AuthEventQuery query
    )
    {
        if (query.Type.HasValue)
            predicate = predicate.And(x => x.Type == query.Type);
        if (query.Success.HasValue)
            predicate = predicate.And(x => x.Success == query.Success);
        if (query.UserId.HasValue)
            predicate = predicate.And(x => x.UserId == query.UserId);
        if (!string.IsNullOrEmpty(query.IpAddress))
            predicate = predicate.And(x => x.IpAddress == query.IpAddress);
        return predicate;
    }
}