using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;
using Digital.Net.Lib.Templating.Attributes;
using Digital.Net.Lib.Templating.Models;

namespace Digital.Net.Lib.Templating;

public static partial class TemplateInterpolator
{
    // A token is a chain of one or more "source.field" terms separated by "??". The capture holds the whole chain.
    [GeneratedRegex(
        @"\{\{\s*([a-zA-Z][a-zA-Z0-9_]*\.[a-zA-Z_][a-zA-Z0-9_]*(?:\s*\?\?\s*[a-zA-Z][a-zA-Z0-9_]*\.[a-zA-Z_][a-zA-Z0-9_]*)*)\s*\}\}"
    )]
    private static partial Regex TokenRegex();

    private const string FallbackSeparator = "??";

    private static readonly ConcurrentDictionary<Type, IReadOnlyList<TemplateVariableDescriptor>> SourceVariables = new();
    private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, PropertyInfo>> SourceFields = new();
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<PropertyInfo>> TargetProperties = new();

    /// <summary>
    ///     Lists tokens exposed by a source entity type.
    /// </summary>
    public static IReadOnlyList<TemplateVariableDescriptor> GetVariables<TSource>() where TSource : class =>
        GetVariables(typeof(TSource));

    /// <summary>
    ///     Lists tokens exposed by a source entity type.
    /// </summary>
    public static IReadOnlyList<TemplateVariableDescriptor> GetVariables(Type sourceType) =>
        SourceVariables.GetOrAdd(sourceType, BuildVariables);

    /// <summary>
    ///     Rewrite a single template string against a source dictionary. Case-insensitive.
    ///     Unknown tokens are left untouched, null source fields are replaced by an empty string.
    /// </summary>
    public static string? Interpolate(string? template, IReadOnlyDictionary<string, object> sources) =>
        string.IsNullOrEmpty(template)
            ? template
            : TokenRegex().Replace(template, match => ResolveToken(match, sources));

    /// <summary>
    ///     Walks all <see cref="TemplateTargetAttribute" /> string properties of <paramref name="target" />
    ///     and rewrites their values against a source dictionary. Case-insensitive.
    ///     Unknown tokens are left untouched, null source fields are replaced by an empty string.
    /// </summary>
    public static void Interpolate<TTarget>(TTarget target, IReadOnlyDictionary<string, object> sources)
        where TTarget : class
    {
        foreach (var property in GetTargetProperties(target.GetType()))
        {
            var current = (string?)property.GetValue(target);
            var hydrated = Interpolate(current, sources);
            if (!string.Equals(current, hydrated, StringComparison.Ordinal))
                property.SetValue(target, hydrated);
        }
    }

    private static string ResolveToken(Match match, IReadOnlyDictionary<string, object> sources)
    {
        var hasResolved = false;

        // Walks the terms left to right and returns the first that holds a value.
        foreach (var term in match.Groups[1].Value.Split(FallbackSeparator, StringSplitOptions.TrimEntries))
        {
            var separator = term.IndexOf('.');
            var objectName = term[..separator].ToLowerInvariant();
            if (!sources.TryGetValue(objectName, out var sourceInstance))
                continue;

            var fields = GetSourceFields(sourceInstance.GetType());
            var fieldName = term[(separator + 1)..].ToLowerInvariant();
            if (!fields.TryGetValue(fieldName, out var property))
                continue;

            hasResolved = true;
            var value = property.GetValue(sourceInstance)?.ToString();
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return hasResolved ? string.Empty : match.Value;
    }

    private static IReadOnlyList<TemplateVariableDescriptor> BuildVariables(Type sourceType)
    {
        var sourceKey = sourceType.Name.ToLowerInvariant();
        return sourceType
            .GetProperties()
            .Where(IsSourceString)
            .Select(p => new TemplateVariableDescriptor(
                $"{{{{ {sourceKey}.{p.Name.ToLowerInvariant()} }}}}",
                sourceType.Name,
                p.Name
            ))
            .ToList();
    }

    private static IReadOnlyDictionary<string, PropertyInfo> GetSourceFields(Type sourceType) =>
        SourceFields.GetOrAdd(sourceType, BuildSourceFields);

    private static IReadOnlyDictionary<string, PropertyInfo> BuildSourceFields(Type sourceType) =>
        sourceType.GetProperties()
            .Where(IsSourceString)
            .ToDictionary(p => p.Name.ToLowerInvariant(), p => p);

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
