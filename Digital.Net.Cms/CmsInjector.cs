using Digital.Net.Cms.Context;
using Digital.Net.Cms.Services;
using Digital.Net.Core;
using Digital.Net.Lib.Configuration;
using Digital.Net.Lib.Entities.Bootstrap;
using Digital.Net.Lib.Entities.Templating;
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

        // No entity of this context declares [TemplateHost] any more — interpolation sources are a
        // client concern. The call stays because the scan is per context: a client app registers its
        // own the same way, and the CMS would pick one up again if it ever declared one.
        builder.AddTemplateSources<CmsContext>();

        builder.Services
            .AddScoped<MediaService>();

        return builder;
    }
}
