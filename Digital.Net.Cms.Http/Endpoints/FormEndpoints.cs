using System.Linq.Expressions;
using System.Text.Json;
using Digital.Net.Cms.Context;
using Digital.Net.Cms.Http.Dto;
using Digital.Net.Cms.Models.Forms;
using Digital.Net.Core.Http.Security;
using Digital.Net.Core.Http.Services.Authentication.Filters;
using Digital.Net.Core.Http.Services.Crud;
using Digital.Net.Core.Http.Services.Pagination.Extensions;
using Digital.Net.Lib.Entities.Exceptions;
using Digital.Net.Lib.Exceptions.types;
using Digital.Net.Lib.Messages;
using Digital.Net.Lib.Predicates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Digital.Net.Cms.Http.Endpoints;

public static class FormEndpoints
{
    private const int MaxFieldValueLength = 4096;
    private const int MaxSerializedValuesLength = 16_384;

    public static IEndpointRouteBuilder MapCmsFormEndpoints(this IEndpointRouteBuilder app)
    {
        var form = app
            .MapGroup("cms/forms")
            .WithTags("CMS.Forms")
            .RequireRateLimiting(RateLimiter.Policy)
            .RequireAuthentication(AuthorizeType.Session | AuthorizeType.ApiKey);

        form.MapCrudSchema<CmsContext, Form>();
        form.MapCrudGet<CmsContext, Form, FormDto>();
        form.MapPaginationGet<CmsContext, Form, FormListDto, FormQuery>(filter: PaginationFilter);
        form.MapCrudPost<CmsContext, Form, FormCreatePayload>();
        form.MapCrudDelete<CmsContext, Form>();
        form.MapCrudPatch<CmsContext, Form>();

        form
            .MapPost("{formId:guid}/fields", CreateFormField)
            .WithSummary("CreateFormField")
            .WithDescription("Creates a new field attached to the form identified by formId.");

        form
            .MapPatch("{formId:guid}/fields/{fieldId:guid}", PatchFormField)
            .WithSummary("PatchFormField")
            .WithDescription(
                "Applies a JSON Patch to a field owned by the form. Returns 404 if the field does not belong to the form.");

        form
            .MapDelete("{formId:guid}/fields/{fieldId:guid}", DeleteFormField)
            .WithSummary("DeleteFormField")
            .WithDescription(
                "Deletes a field owned by the form. Returns 404 if the field does not belong to the form.");


        var submissions = app
            .MapGroup("cms/forms/submissions")
            .WithTags("CMS.FormsSubmissions")
            .RequireRateLimiting(RateLimiter.Policy)
            .RequireAuthentication(AuthorizeType.Session | AuthorizeType.ApiKey);

        submissions.MapCrudGet<CmsContext, FormSubmission, FormSubmissionDto>();
        submissions.MapPaginationGet<CmsContext, FormSubmission, FormSubmissionDto, FormSubmissionQuery>(
            filter: SubmissionPaginationFilter
        );
        submissions.MapCrudDelete<CmsContext, FormSubmission>();
        
        return app;
    }
    
    private static async Task<Results<Ok<Result<Guid>>, BadRequest<Result<Guid>>, NotFound<Result<Guid>>,
            InternalServerError<Result<Guid>>>>
        CreateFormField(
            Guid formId,
            [FromBody]
            FormFieldPayload payload,
            CmsContext context,
            CrudService<CmsContext, FormField> crudService,
            CancellationToken ct
        )
    {
        var formExists = await context.Forms.AsNoTracking().AnyAsync(f => f.Id == formId, ct);
        if (!formExists)
            return TypedResults.NotFound(new Result<Guid>().AddError(new ResourceNotFoundException()));

        var entity = new FormField
        {
            FormId = formId,
            Name = payload.Name,
            Type = payload.Type,
            Label = payload.Label,
            Placeholder = payload.Placeholder,
            DefaultValue = payload.DefaultValue,
            Required = payload.Required,
            SortOrder = payload.SortOrder,
            ValidationJson = payload.ValidationJson,
            OptionsJson = payload.OptionsJson
        };

        var result = await crudService.Create(entity, ct);
        var isBadRequest = result.HasErrorOfType<EntityValidationException>();
        if (result.HasError && !isBadRequest)
            return TypedResults.InternalServerError(result);

        return result.HasError
            ? TypedResults.BadRequest(result)
            : TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<Result>, BadRequest<Result>, NotFound<Result>, InternalServerError<Result>>>
        PatchFormField(
            Guid formId,
            Guid fieldId,
            [FromBody]
            JsonElement patch,
            CmsContext context,
            CrudService<CmsContext, FormField> crudService,
            CancellationToken ct
        )
    {
        var field = await context.FormFields.AsNoTracking().FirstOrDefaultAsync(f => f.Id == fieldId, ct);
        if (field is null || field.FormId != formId)
            return TypedResults.NotFound(new Result().AddError(new ResourceNotFoundException()));

        if (PatchTargetsFormId(patch))
            return TypedResults.BadRequest(
                new Result().AddError(new EntityValidationException("FormId cannot be patched."))
            );

        var result = await crudService.Patch(patch, fieldId, ct);
        var isBadRequest = result.HasErrorOfType<EntityValidationException>() ||
                           result.HasErrorOfType<InvalidOperationException>();

        if (result.HasErrorOfType<ResourceNotFoundException>())
            return TypedResults.NotFound(result);
        if (result.HasError && !isBadRequest)
            return TypedResults.InternalServerError(result);

        return result.HasError
            ? TypedResults.BadRequest(result)
            : TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<Result>, NotFound<Result>, InternalServerError<Result>>>
        DeleteFormField(
            Guid formId,
            Guid fieldId,
            CmsContext context,
            CrudService<CmsContext, FormField> crudService,
            CancellationToken ct
        )
    {
        var field = await context.FormFields.AsNoTracking().FirstOrDefaultAsync(f => f.Id == fieldId, ct);
        if (field is null || field.FormId != formId)
            return TypedResults.NotFound(new Result().AddError(new ResourceNotFoundException()));

        var result = await crudService.Delete(fieldId, ct);
        if (result.HasErrorOfType<ResourceNotFoundException>())
            return TypedResults.NotFound(result);
        if (result.HasError)
            return TypedResults.InternalServerError(result);

        return TypedResults.Ok(result);
    }

    private static bool PatchTargetsFormId(JsonElement patch)
    {
        if (patch.ValueKind != JsonValueKind.Array)
            return false;
        foreach (var operation in patch.EnumerateArray())
        {
            if (operation.ValueKind != JsonValueKind.Object)
                continue;
            if (!operation.TryGetProperty("path", out var pathEl) || pathEl.ValueKind != JsonValueKind.String)
                continue;
            var path = pathEl.GetString();
            if (string.Equals(path, "/formId", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(path, "/FormId", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static Expression<Func<Form, bool>> PaginationFilter(
        Expression<Func<Form, bool>> predicate,
        FormQuery query
    )
    {
        if (!string.IsNullOrEmpty(query.Name))
            predicate = predicate.And(x => x.Name.StartsWith(query.Name));
        if (query.Published.HasValue)
            predicate = predicate.And(x => x.Published == query.Published);
        if (!string.IsNullOrEmpty(query.Path))
            predicate = predicate.And(x => x.Path == query.Path);
        return predicate;
    }

    private static Expression<Func<FormSubmission, bool>> SubmissionPaginationFilter(
        Expression<Func<FormSubmission, bool>> predicate,
        FormSubmissionQuery query
    )
    {
        if (query.FormId.HasValue)
            predicate = predicate.And(x => x.FormId == query.FormId);
        return predicate;
    }
}