using System.Linq.Expressions;

namespace Digital.Net.Cms.Models.Pages;

public static class PageVisibility
{
    /// <summary>
    ///     What it takes for a page to be served: published, and either due or on no schedule at all.
    ///     Written as an expression so every caller filters in SQL — a page held back by its date must
    ///     never reach a count, a sitemap or a public lookup, not even to be dropped afterwards.
    /// </summary>
    public static Expression<Func<Page, bool>> IsLive(DateTime now) =>
        page => page.Published && (page.PublishedAt == null || page.PublishedAt <= now);
}
