using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using Digital.Net.Lib.Entities.Models;
using Digital.Net.Lib.Entities.Projection;
using Digital.Net.Lib.Predicates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Digital.Net.Core.Http.Services.Pagination.Extensions;

public static class PaginationEndpointExtensions
{
    public static RouteHandlerBuilder MapPaginationGet<TContext, T, TDto, TQuery>(
        this IEndpointRouteBuilder app,
        string? route = null,
        Func<Expression<Func<T, bool>>, TQuery, Expression<Func<T, bool>>>? filter = null,
        Expression<Func<T, TDto>>? projection = null
    )
        where TContext : DbContext
        where T : Entity
        where TDto : class
        where TQuery : Query =>
        app
            .MapGet(route ?? "", async Task<Results<Ok<QueryResult<TDto>>, BadRequest<QueryResult<TDto>>>> (
                [AsParameters]
                TQuery query,
                TContext context,
                CancellationToken ct
            ) =>
            {
                query.ValidateParameters();
                var result = new QueryResult<TDto>();

                string orderClause;
                try
                {
                    orderClause = OrderByResolver.ResolveOrderClause<T>(query.OrderBy, query.Order);
                }
                catch (InvalidOrderByException e)
                {
                    result.AddError(e);
                    return TypedResults.BadRequest(result);
                }

                try
                {
                    var predicate = BuildFilter(query, filter);
                    var items = context.Set<T>().Where(predicate);
                    var rowCount = await items.CountAsync(ct);

                    var config = new ParsingConfig { IsCaseSensitive = false };

                    items = items.AsNoTracking();
                    items = items.OrderBy(config, orderClause);
                    items = items.Skip((query.Index - 1) * query.Size).Take(query.Size);

                    result.Value = projection is not null
                        ? await items.Select(projection).ToListAsync(ct)
                        : await items.ProjectTo<T, TDto>().ToListAsync(ct);

                    result.Total = rowCount;
                    result.Index = query.Index;
                    result.Size = query.Size;
                }
                catch (Exception e)
                {
                    result.AddError(e);
                }

                return TypedResults.Ok(result);
            })
            .WithSummary($"GetPaginated: {typeof(T).Name}")
            .WithDescription(
                $"Retrieves a paginated list of {typeof(T).Name}s based on the provided query parameters. " +
                $"Returns a QueryResult containing the paginated {typeof(T).Name}s and metadata."
            );

    private static Expression<Func<T, bool>> BuildFilter<T, TQuery>(
        TQuery query,
        Func<Expression<Func<T, bool>>, TQuery, Expression<Func<T, bool>>>? filter
    )
        where T : Entity
        where TQuery : Query
    {
        var predicate = PredicateBuilder.New<T>().ApplyDateRange(query);
        if (filter is not null)
            predicate = filter(predicate, query);
        return predicate;
    }

    public static Expression<Func<T, bool>> ApplyDateRange<T>(this Expression<Func<T, bool>> predicate, Query query)
        where T : Entity
    {
        if (query.CreatedFrom.HasValue)
            predicate = predicate.And(x => x.CreatedAt >= query.CreatedFrom);
        if (query.UpdatedFrom.HasValue)
            predicate = predicate.And(x => x.UpdatedAt >= query.UpdatedFrom);
        if (query.CreatedTo is not null)
            predicate = predicate.And(x => x.CreatedAt <= query.CreatedTo);
        if (query.UpdatedTo is not null)
            predicate = predicate.And(x => x.UpdatedAt <= query.UpdatedTo);
        return predicate;
    }
}