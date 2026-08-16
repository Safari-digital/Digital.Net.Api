using Digital.Net.Lib.Entities.Models;

namespace Digital.Net.Lib.Entities.Templating;

public interface ITemplateSourceResolver
{
    Type SourceType { get; }

    Task<Entity?> ResolveAsync(Guid hostId, CancellationToken ct = default);
}