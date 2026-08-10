using Digital.Net.Lib.Entities.Models;

namespace Digital.Net.Cms.Templating;

/// <summary>
///     Reads the instances of one declared interpolation source. One implementation is registered per
///     source, so the library resolves a page's source without knowing any concrete entity type.
/// </summary>
public interface ITemplateSourceResolver
{
    Type SourceType { get; }

    /// <summary>Returns the instance attached to one of <paramref name="pageIds" />, or null.</summary>
    Task<Entity?> ResolveAsync(IReadOnlyList<Guid> pageIds, CancellationToken ct = default);

    /// <summary>Returns every instance attached to <paramref name="pageId" />.</summary>
    Task<IReadOnlyList<Entity>> ListForPageAsync(Guid pageId, CancellationToken ct = default);
}
