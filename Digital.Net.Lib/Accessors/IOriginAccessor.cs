namespace Digital.Net.Lib.Accessors;

public interface IOriginAccessor
{
    /// <summary>
    ///     Get the current request origin. Throws <see cref="ArgumentNullException" /> if no HTTP context is available.
    /// </summary>
    RequestOrigin GetOrigin();

    /// <summary>
    ///     Try to get the current request origin. Returns <see cref="RequestOrigin" /> with null fields if no HTTP
    ///     context is available.
    /// </summary>
    RequestOrigin TryGetOrigin();
}