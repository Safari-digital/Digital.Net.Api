using System.Reflection;
using Digital.Net.Lib.Entities.Attributes;
using Digital.Net.Lib.Entities.Models;
using Digital.Net.Lib.Entities.Pivots;

namespace Digital.Net.Cms.Templating;

/// <summary>
///     Reads the interpolation sources declared by an assembly. Declarations are validated here, at
///     discovery time, so a malformed <see cref="TemplateHostAttribute" /> fails the bootstrap rather
///     than the first request that happens to need it.
/// </summary>
public static class TemplateSourceScanner
{
    public static IEnumerable<TemplateSourceDescriptor> Discover(Assembly assembly)
    {
        foreach (var type in PivotReflection.SafeGetTypes(assembly))
        {
            if (type is not { IsClass: true, IsAbstract: false } || !typeof(Entity).IsAssignableFrom(type))
                continue;

            var navigations = type.GetProperties()
                .Where(property => property.GetCustomAttribute<TemplateHostAttribute>() is not null)
                .ToList();

            if (navigations.Count == 0)
                continue;

            if (navigations.Count > 1)
                throw new InvalidOperationException(
                    $"Template source '{type.Name}' carries [TemplateHost] on several navigations "
                    + $"({string.Join(", ", navigations.Select(n => n.Name))}); a source hosts on exactly one page."
                );

            var navigation = navigations[0];
            var host = navigation.GetCustomAttribute<TemplateHostAttribute>()!;

            yield return new TemplateSourceDescriptor(
                type,
                navigation.Name,
                ValidateDiscriminator(type, host.Discriminator),
                host.PublishedFlag,
                IsBooleanPublishedFlag(type, host.PublishedFlag)
            );
        }
    }

    private static string? ValidateDiscriminator(Type type, string? discriminator)
    {
        if (discriminator is null)
            return null;

        var property = type.GetProperty(discriminator)
                       ?? throw new InvalidOperationException(
                           $"Template source '{type.Name}' declares the discriminator '{discriminator}', "
                           + "which is not one of its properties."
                       );

        if (property.PropertyType != typeof(string))
            throw new InvalidOperationException(
                $"Discriminator '{type.Name}.{discriminator}' is a {property.PropertyType.Name}; "
                + "template resolution matches it against a string and only supports string."
            );

        return discriminator;
    }

    private static bool IsBooleanPublishedFlag(Type type, string? publishedFlag)
    {
        if (publishedFlag is null)
            return false;

        var property = type.GetProperty(publishedFlag)
                       ?? throw new InvalidOperationException(
                           $"Template source '{type.Name}' declares the publication flag '{publishedFlag}', "
                           + "which is not one of its properties."
                       );

        if (property.PropertyType == typeof(bool))
            return true;

        if (property.PropertyType == typeof(DateTime?))
            return false;

        throw new InvalidOperationException(
            $"Publication flag '{type.Name}.{publishedFlag}' is a {property.PropertyType.Name}; "
            + "only bool and DateTime? are supported."
        );
    }
}
