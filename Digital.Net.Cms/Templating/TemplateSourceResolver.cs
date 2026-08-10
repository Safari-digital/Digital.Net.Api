using Digital.Net.Lib.Entities.Models;
using Microsoft.EntityFrameworkCore;

namespace Digital.Net.Cms.Templating;

/// <summary>
///     The single generic resolver backing every declared source. It reaches the client data through the
///     typed <see cref="DbSet{TEntity}" /> of the context supplied by the caller, so the library never
///     names a client context nor reflects over its data at query time.
/// </summary>
public class TemplateSourceResolver<TContext, TEntity>(TContext context, TemplateSourceDescriptor descriptor)
    : ITemplateSourceResolver
    where TContext : DbContext
    where TEntity : Entity
{
    public Type SourceType => typeof(TEntity);

    public async Task<Entity?> ResolveAsync(
        IReadOnlyList<Guid> pageIds,
        string? discriminator,
        CancellationToken ct = default
    )
    {
        if (pageIds.Count == 0)
            return null;

        var query = AttachedTo(pageIds);
        if (descriptor.Discriminator is not null)
        {
            // The source cannot be told apart without it: resolving anything here would return an
            // arbitrary instance among those sharing the page.
            if (string.IsNullOrEmpty(discriminator))
                return null;

            var name = descriptor.Discriminator;
            query = query.Where(e => EF.Property<string>(e, name) == discriminator);
        }

        return await query.FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<Entity>> ListForPageAsync(Guid pageId, CancellationToken ct = default) =>
        await AttachedTo([pageId]).ToListAsync<Entity>(ct);

    public string? GetDiscriminatorValue(Entity source) =>
        descriptor.Discriminator is null
            ? null
            : source.GetType().GetProperty(descriptor.Discriminator)?.GetValue(source)?.ToString();

    private IQueryable<TEntity> AttachedTo(IReadOnlyList<Guid> pageIds)
    {
        var foreignKey = descriptor.ForeignKey;
        // Nullable so an unattached source never matches a page id.
        var ids = pageIds.Select(id => (Guid?)id).ToList();
        var query = context.Set<TEntity>()
            .AsNoTracking()
            .Where(e => ids.Contains(EF.Property<Guid?>(e, foreignKey)));

        if (descriptor.PublishedFlag is null)
            return query;

        var flag = descriptor.PublishedFlag;
        return descriptor.PublishedFlagIsBoolean
            ? query.Where(e => EF.Property<bool>(e, flag))
            : query.Where(e => EF.Property<DateTime?>(e, flag) != null);
    }

}
