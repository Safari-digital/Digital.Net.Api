using System.Linq.Expressions;

namespace Digital.Net.Cms.Models.Pages;

public static class PageExpressions
{
    public static Expression<Func<Page, bool>> IsLive() =>
        page => page.Published && (page.PublishedAt == null || page.PublishedAt <= DateTime.UtcNow);
}