using Digital.Net.Lib.Entities.Models;

namespace Digital.Net.Cms.Templating;

/// <summary>
///     Reads the instances of one declared interpolation source. One implementation is registered per
///     source, so the library resolves a page's source without knowing any concrete entity type.
/// </summary>
public interface ITemplateSourceResolver
{
    Type SourceType { get; }

    /// <summary>
    ///     Returns the published instance attached to one of <paramref name="pageIds" />, or null.
    ///     When the source declares a discriminator, <paramref name="discriminator" /> must match it —
    ///     a null value then resolves nothing, since the pages cannot tell their sources apart.
    /// </summary>
    Task<Entity?> ResolveAsync(
        IReadOnlyList<Guid> pageIds,
        string? discriminator,
        CancellationToken ct = default
    );

    /// <summary>Returns every published instance attached to <paramref name="pageId" />.</summary>
    Task<IReadOnlyList<Entity>> ListForPageAsync(Guid pageId, CancellationToken ct = default);

    /// <summary>Reads the discriminator of an instance, or null when the source declares none.</summary>
    string? GetDiscriminatorValue(Entity source);
}
