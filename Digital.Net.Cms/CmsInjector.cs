using Digital.Net.Cms.Context;
using Digital.Net.Cms.Services;
using Digital.Net.Cms.Templating;
using Digital.Net.Core;
using Digital.Net.Lib.Configuration;
using Digital.Net.Lib.Entities.Bootstrap;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Digital.Net.Cms;

public static class CmsInjector
{
    /// <summary>
    ///     Registers the Digital.Net CMS business layer (CmsContext, migrations, domain services).
    ///     HTTP wiring lives in Digital.Net.Cms.Http (AddDigitalNetCmsHttp / UseDigitalNetCmsHttp).
    /// </summary>
    public static TBuilder AddDigitalNetCms<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.AddDatabaseContext<CmsContext>(
            builder.Configuration.GetOrThrow<string>(CoreSettings.ConnectionStringKey));

        // Article still lives in CmsContext, so the CMS declares its own sources. A client app declares
        // its context the same way — the mechanism does not change when Article moves out.
        builder.AddTemplateSources<CmsContext>();

        builder.Services
            .AddScoped<MediaService>();

        return builder;
    }
}
