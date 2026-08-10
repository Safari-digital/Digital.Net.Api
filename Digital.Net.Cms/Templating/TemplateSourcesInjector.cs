using Digital.Net.Lib.Entities.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Digital.Net.Cms.Templating;

public static class TemplateSourcesInjector
{
    /// <summary>
    ///     Declares the interpolation sources of a context: scans its assembly, keeps the entities whose
    ///     navigation carries <see cref="TemplateHostAttribute" />, and registers a closed instance of the
    ///     generic resolver for each. The library never learns the name of the context — the caller
    ///     supplies its type and data is reached through the typed <see cref="DbSet{TEntity}" />.
    /// </summary>
    public static IHostApplicationBuilder AddTemplateSources<TContext>(this IHostApplicationBuilder builder)
        where TContext : DbContext
    {
        foreach (var descriptor in TemplateSourceScanner.Discover(typeof(TContext).Assembly))
        {
            builder.Services.AddSingleton(descriptor);
            builder.Services.AddScoped<ITemplateSourceResolver>(provider =>
                (ITemplateSourceResolver)ActivatorUtilities.CreateInstance(
                    provider,
                    typeof(TemplateSourceResolver<,>).MakeGenericType(typeof(TContext), descriptor.SourceType),
                    descriptor
                )
            );
        }

        return builder;
    }
}
