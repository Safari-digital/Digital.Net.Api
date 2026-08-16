using System.Reflection;
using Digital.Net.Lib.Entities.Models;
using Digital.Net.Lib.Entities.Pivots;
using Digital.Net.Lib.Templating.Attributes;
using Digital.Net.Lib.Templating.Models;

namespace Digital.Net.Lib.Entities.Templating;

public static class TemplateSourceScanner
{
    /// <summary>
    ///     Reads the interpolation sources declared by an assembly. Declarations are validated at
    ///     discovery time, so a malformed <see cref="TemplateHostAttribute" /> fails the bootstrap rather
    ///     than the first request that uses it.
    /// </summary>
    public static IEnumerable<TemplateSourceDescriptor> Discover(Assembly assembly) =>
        Discover(PivotReflection.SafeGetTypes(assembly));

    public static IEnumerable<TemplateSourceDescriptor> Discover(IEnumerable<Type> types)
    {
        foreach (var type in types)
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
                    + $"({string.Join(", ", keys.Select(k => k.Name))}); a source hosts on exactly one entity."
                );

            var foreignKey = keys[0];
            if (foreignKey.PropertyType != typeof(Guid?) && foreignKey.PropertyType != typeof(Guid))
                throw new InvalidOperationException(
                    $"[TemplateHost] on '{type.Name}.{foreignKey.Name}' is a {foreignKey.PropertyType.Name}; "
                    + "it must sit on the Guid foreign key holding the id of the hosting entity."
                );

            yield return new TemplateSourceDescriptor(type, foreignKey.Name);
        }
    }
}