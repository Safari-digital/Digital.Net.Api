using Digital.Net.Lib.Entities.Models;
using Digital.Net.Lib.Templating.Models;
using Microsoft.EntityFrameworkCore;

namespace Digital.Net.Lib.Entities.Templating;

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
    ///     Every source owns a dedicated host, so the foreign key alone identifies it — no discriminator,
    ///     and no visibility filter either: any such rule belongs to the host, which is only resolved when
    ///     it passes it.
    /// </summary>
    public async Task<Entity?> ResolveAsync(Guid hostId, CancellationToken ct = default)
    {
        var foreignKey = descriptor.ForeignKey;
        // Nullable so an unattached source never matches a host id.
        var id = (Guid?)hostId;
        return await context.Set<TEntity>()
            .AsNoTracking()
            .Where(e => EF.Property<Guid?>(e, foreignKey) == id)
            .FirstOrDefaultAsync(ct);
    }
}