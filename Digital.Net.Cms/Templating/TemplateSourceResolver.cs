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

    /// <summary>
    ///     Every source owns a dedicated page, so the foreign key alone identifies it — no discriminator,
    ///     and no publication filter either: the page carries <c>Published</c> and is only resolved when
    ///     it is.
    /// </summary>
    public async Task<Entity?> ResolveAsync(IReadOnlyList<Guid> pageIds, CancellationToken ct = default) =>
        pageIds.Count == 0 ? null : await AttachedTo(pageIds).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Entity>> ListForPageAsync(Guid pageId, CancellationToken ct = default) =>
        await AttachedTo([pageId]).ToListAsync<Entity>(ct);

    private IQueryable<TEntity> AttachedTo(IReadOnlyList<Guid> pageIds)
    {
        var foreignKey = descriptor.ForeignKey;
        // Nullable so an unattached source never matches a page id.
        var ids = pageIds.Select(id => (Guid?)id).ToList();
        return context.Set<TEntity>()
            .AsNoTracking()
            .Where(e => ids.Contains(EF.Property<Guid?>(e, foreignKey)));
    }
}
