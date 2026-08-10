using Digital.Net.Cms.Context;
using Digital.Net.Cms.Models.Pages;
using Digital.Net.Lib.Random;

namespace Digital.Net.Tests.Core.Factories.Data;

public static class TestPageFactory
{
    public static Page BuildTestPage(
        this CmsContext context,
        string? path = null,
        bool published = false,
        bool? indexed = true
    )
    {
        var page = new Page
        {
            Path = path ?? $"/{Randomizer.GenerateRandomString(Randomizer.AnyLetterOrNumber, 10)}",
            Published = published,
            Indexed = indexed!.Value
        };
        context.Pages.Add(page);
        context.SaveChanges();
        return page;
    }
}
