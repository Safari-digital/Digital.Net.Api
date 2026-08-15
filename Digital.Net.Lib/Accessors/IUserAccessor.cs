namespace Digital.Net.Lib.Accessors;

public interface IUserAccessor
{
    /// <summary>Get the current user ID. Throws <see cref="UnauthorizedAccessException" /> if not authenticated.</summary>
    Guid GetUserId();

    /// <summary>Try to get the current user ID. Returns <c>null</c> if no user is found.</summary>
    Guid? TryGetUserId();
}