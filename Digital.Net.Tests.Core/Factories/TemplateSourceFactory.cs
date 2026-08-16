using System;
using System.Collections.Generic;
using System.Linq;
using Digital.Net.Cms.Context;
using Digital.Net.Lib.Entities.Templating;

namespace Digital.Net.Tests.Core.Factories;

public static class TemplateSourceFactory
{
    /// <summary>
    ///     Mirrors AddTemplateSources for the service tests, which compose their dependencies by hand
    ///     instead of going through a DI container.
    /// </summary>
    public static IReadOnlyList<ITemplateSourceResolver> BuildTemplateSourceResolvers(this CmsContext context) =>
        TemplateSourceScanner.Discover(typeof(CmsContext).Assembly)
            .Select(descriptor => (ITemplateSourceResolver)Activator.CreateInstance(
                typeof(TemplateSourceResolver<,>).MakeGenericType(typeof(CmsContext), descriptor.SourceType),
                context,
                descriptor
            )!)
            .ToList();
}
