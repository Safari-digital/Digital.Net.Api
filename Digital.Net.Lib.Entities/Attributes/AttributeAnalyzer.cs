using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Text.RegularExpressions;
using Digital.Net.Lib.Abstractions;
using Digital.Net.Lib.Entities.Models;
using Digital.Net.Lib.Templating.Attributes;
using Microsoft.EntityFrameworkCore;

namespace Digital.Net.Lib.Entities.Attributes;

/// <summary>
///     Digital.Net <see cref="Entity" /> Attribute analyzer.
/// </summary>
public static class AttributeAnalyzer<T>
    where T : class, IEntity
{
    public static bool IsRequired(string propertyName) =>
        typeof(T).GetProperty(propertyName)?.GetCustomAttribute<RequiredAttribute>() is not null;

    public static bool IsRequired(PropertyInfo property) =>
        property.GetCustomAttribute<RequiredAttribute>() is not null;

    public static bool IsUnique(string propertyName) =>
        typeof(T)
            .GetCustomAttributes<IndexAttribute>()
            .Any(attr => attr.IsUnique && attr.PropertyNames.Contains(propertyName));

    public static bool IsUnique(PropertyInfo property) =>
        typeof(T)
            .GetCustomAttributes<IndexAttribute>()
            .Any(attr => attr.IsUnique && attr.PropertyNames.Contains(property.Name));

    public static int MaxLength(string propertyName) =>
        typeof(T).GetProperty(propertyName)?.GetCustomAttribute<MaxLengthAttribute>()?.Length ?? 0;

    public static int? MaxLength(PropertyInfo property) =>
        property.GetCustomAttribute<MaxLengthAttribute>()?.Length;

    public static bool IsIdentity(string propertyName) =>
        typeof(T)
            .GetProperty(propertyName)
            ?.GetCustomAttribute<DatabaseGeneratedAttribute>()
            ?.DatabaseGeneratedOption == DatabaseGeneratedOption.Identity;

    public static bool IsIdentity(PropertyInfo property) =>
        property.GetCustomAttribute<DatabaseGeneratedAttribute>()?.DatabaseGeneratedOption
        == DatabaseGeneratedOption.Identity;

    public static bool IsForeignKey(string propertyName) =>
        typeof(T).GetProperty(propertyName)?.GetCustomAttribute<ForeignKeyAttribute>() is not null;

    public static bool IsForeignKey(PropertyInfo property) =>
        property.GetCustomAttribute<ForeignKeyAttribute>() is not null;

    public static bool IsSecret(string propertyName) =>
        typeof(T).GetProperty(propertyName)?.GetCustomAttribute<SecretAttribute>() is not null;

    public static bool IsSecret(PropertyInfo property) =>
        property.GetCustomAttribute<SecretAttribute>() is not null;

    public static bool IsReadOnly(string propertyName) =>
        typeof(T).GetProperty(propertyName)?.GetCustomAttribute<ReadOnlyAttribute>() is not null;

    public static bool IsReadOnly(PropertyInfo property) =>
        property.GetCustomAttribute<ReadOnlyAttribute>() is not null;

    public static bool IsSortable(string propertyName) =>
        typeof(T).GetProperty(propertyName)?.GetCustomAttribute<SortableAttribute>() is not null;

    public static bool IsSortable(PropertyInfo property) =>
        property.GetCustomAttribute<SortableAttribute>() is not null;

    public static string GetPath(string propertyName) =>
        typeof(T).GetProperty(propertyName)?.GetCustomAttribute<ColumnAttribute>()?.Name
        ?? propertyName;

    public static string GetPath(PropertyInfo property) =>
        property.GetCustomAttribute<ColumnAttribute>()?.Name ?? property.Name;

    public static bool IsTemplateTarget(string propertyName) =>
        typeof(T).GetProperty(propertyName)?.GetCustomAttribute<TemplateTargetAttribute>() is not null;

    public static bool IsTemplateTarget(PropertyInfo property) =>
        property.GetCustomAttribute<TemplateTargetAttribute>() is not null;

    public static bool IsTemplateSource(string propertyName) =>
        typeof(T).GetProperty(propertyName)?.GetCustomAttribute<TemplateSourceAttribute>() is not null;

    public static bool IsTemplateSource(PropertyInfo property) =>
        property.GetCustomAttribute<TemplateSourceAttribute>() is not null;

    public static Regex? GetRegex(string propertyName) =>
        typeof(T).GetProperty(propertyName)?.GetCustomAttribute<RegexValidationAttribute>()?.Regex;

    public static Regex? GetRegex(PropertyInfo property) =>
        property.GetCustomAttribute<RegexValidationAttribute>()?.Regex;

    public static IReadOnlyList<string>? GetOneOf(string propertyName) =>
        typeof(T).GetProperty(propertyName)?.GetCustomAttribute<OneOfAttribute>()?.Values;

    public static IReadOnlyList<string>? GetOneOf(PropertyInfo property) =>
        property.GetCustomAttribute<OneOfAttribute>()?.Values;

    public static bool HasChildSchema(string propertyName) =>
        typeof(T).GetProperty(propertyName)?.GetCustomAttribute<ChildSchemaAttribute>() is not null;

    public static bool HasChildSchema(PropertyInfo property) =>
        property.GetCustomAttribute<ChildSchemaAttribute>() is not null;
}