using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;
using Digital.Net.Lib.Entities.Attributes;
using Digital.Net.Lib.Entities.Models;

namespace Digital.Net.Core.Services.Templating;

/// <summary>
///     Hydrates string templates of the form <c>{{ source.field }}</c>. Fields marked with
///     <see cref="TemplateSourceAttribute" /> feed the tokens; fields marked with
///     <see cref="TemplateTargetAttribute" /> hold the templates being rewritten.
/// </summary>
public static partial class TemplateInterpolator
{
    [GeneratedRegex(@"\{\{\s*([a-z][a-zA-Z0-9_]*)\.([a-zA-Z_][a-zA-Z0-9_]*)\s*\}\}")]
    private static partial Regex TokenRegex();

    private static readonly ConcurrentDictionary<Type, IReadOnlyList<TemplateVariableDescriptor>> SourceVariables = new();
    private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, PropertyInfo>> SourceFields = new();
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<PropertyInfo>> TargetProperties = new();

    /// <summary>Lists tokens exposed by a source entity type.</summary>
    public static IReadOnlyList<TemplateVariableDescriptor> GetVariables<TSource>() where TSource : Entity =>
        GetVariables(typeof(TSource));

    /// <summary>Lists tokens exposed by a source entity type.</summary>
    public static IReadOnlyList<TemplateVariableDescriptor> GetVariables(Type sourceType) =>
        SourceVariables.GetOrAdd(sourceType, BuildVariables);

    /// <summary>
    ///     Hydrates a single template string against a sources dictionary.
    ///     Keys of <paramref name="sources" /> must be the lowercased source aliases (e.g. <c>"article"</c>).
    ///     Unknown tokens are left untouched; null source fields are replaced by an empty string.
    /// </summary>
    public static string? Interpolate(string? template, IReadOnlyDictionary<string, object> sources) =>
        string.IsNullOrEmpty(template)
            ? template
            : TokenRegex().Replace(template, match => ResolveToken(match, sources));

    /// <summary>
    ///     Walks all <see cref="TemplateTargetAttribute" /> string properties of <paramref name="target" />
    ///     and rewrites their values in place.
    /// </summary>
    public static void HydrateInPlace<TTarget>(TTarget target, IReadOnlyDictionary<string, object> sources)
        where TTarget : class
    {
        foreach (var property in GetTargetProperties(target.GetType()))
        {
            var current = (string?)property.GetValue(target);
            var hydrated = Interpolate(current, sources);
            if (!ReferenceEquals(current, hydrated))
                property.SetValue(target, hydrated);
        }
    }

    private static string ResolveToken(Match match, IReadOnlyDictionary<string, object> sources)
    {
        var sourceKey = match.Groups[1].Value;
        var fieldName = match.Groups[2].Value;

        if (!sources.TryGetValue(sourceKey, out var sourceInstance))
            return match.Value;

        var fields = GetSourceFields(sourceInstance.GetType());
        if (!fields.TryGetValue(fieldName.ToLowerInvariant(), out var property))
            return match.Value;

        return property.GetValue(sourceInstance)?.ToString() ?? string.Empty;
    }

    private static IReadOnlyList<TemplateVariableDescriptor> BuildVariables(Type sourceType)
    {
        var sourceKey = sourceType.Name.ToLowerInvariant();
        return sourceType.GetProperties()
            .Where(IsSourceString)
            .Select(p => new TemplateVariableDescriptor(
                $"{{{{ {sourceKey}.{p.Name.ToLowerInvariant()} }}}}",
                sourceType.Name,
                p.Name))
            .ToList();
    }

    private static IReadOnlyDictionary<string, PropertyInfo> GetSourceFields(Type sourceType) =>
        SourceFields.GetOrAdd(sourceType, BuildSourceFields);

    private static IReadOnlyDictionary<string, PropertyInfo> BuildSourceFields(Type sourceType) =>
        sourceType.GetProperties()
            .Where(IsSourceString)
            .ToDictionary(p => p.Name.ToLowerInvariant(), p => p);

    // Keyed on the runtime type, so an EF proxy gets its own entry. Proxies inherit the attributes of
    // the entity they derive from, so both entries resolve to the same set of properties.
    private static IReadOnlyList<PropertyInfo> GetTargetProperties(Type targetType) =>
        TargetProperties.GetOrAdd(targetType, t => t.GetProperties()
            .Where(p => p.CanWrite && IsTargetString(p))
            .ToList());

    private static bool IsSourceString(PropertyInfo property) =>
        property.PropertyType == typeof(string)
        && property.GetCustomAttribute<TemplateSourceAttribute>() is not null;

    private static bool IsTargetString(PropertyInfo property) =>
        property.PropertyType == typeof(string)
        && property.GetCustomAttribute<TemplateTargetAttribute>() is not null;
}
