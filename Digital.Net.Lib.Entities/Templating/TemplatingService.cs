using Digital.Net.Lib.Entities.Models;

namespace Digital.Net.Lib.Entities.Templating;

public class TemplatingService(
    IEnumerable<ITemplateSourceResolver> sourceResolvers
)
{
    /// <summary>
    ///     Queries the declared sources until one answers. As many round-trips as declared sources.
    /// </summary>
    public async Task<Entity?> ResolveSourceAsync(Guid owner, CancellationToken ct = default)
    {
        foreach (var resolver in sourceResolvers)
        {
            var source = await resolver.ResolveAsync(owner, ct);
            if (source is not null)
                return source;
        }

        return null;
    }
}