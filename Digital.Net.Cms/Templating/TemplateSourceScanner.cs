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

            var keys = type.GetProperties()
                .Where(property => property.GetCustomAttribute<TemplateHostAttribute>() is not null)
                .ToList();

            if (keys.Count == 0)
                continue;

            if (keys.Count > 1)
                throw new InvalidOperationException(
                    $"Template source '{type.Name}' carries [TemplateHost] on several properties "
                    + $"({string.Join(", ", keys.Select(k => k.Name))}); a source hosts on exactly one page."
                );

            var foreignKey = keys[0];
            if (foreignKey.PropertyType != typeof(Guid?) && foreignKey.PropertyType != typeof(Guid))
                throw new InvalidOperationException(
                    $"[TemplateHost] on '{type.Name}.{foreignKey.Name}' is a {foreignKey.PropertyType.Name}; "
                    + "it must sit on the Guid foreign key holding the id of the hosting page."
                );

            yield return new TemplateSourceDescriptor(type, foreignKey.Name);
        }
    }
}
