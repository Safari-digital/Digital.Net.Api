using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using Digital.Net.Lib.Entities.Attributes;
using Digital.Net.Lib.Entities.Exceptions;
using Digital.Net.Lib.Entities.Pivots;

namespace Digital.Net.Lib.Entities.Models;

/// <summary>
///     Serialized shape of an entity property schema, shared by every closed
///     <see cref="SchemaProperty{T}" /> so child schemas can be embedded regardless of their entity type.
/// </summary>
public abstract class SchemaProperty
{
    public string Name { get; init; } = null!;
    public string Path { get; init; } = null!;
    public string Type { get; init; } = null!;
    public bool IsReadOnly { get; init; }
    public bool IsSecret { get; init; }
    public bool IsRequired { get; init; }
    public bool IsUnique { get; init; }
    public bool IsTemplateTarget { get; init; }
    public bool IsTemplateSource { get; init; }
    public int? MaxLength { get; init; }
    public bool IsIdentity { get; init; }
    public bool IsForeignKey { get; init; }
    public string? RegexValidation { get; init; }
    public IReadOnlyList<string>? OneOfValues { get; init; }
    public string[]? EnumValues { get; init; }

    /// <summary>
    ///     Child entity schema embedded on <c>Collection</c> properties whose rows are edited inline
    ///     with the parent: Cascade pivots and navigations carrying <see cref="ChildSchemaAttribute" />.
    ///     Null on scalar properties and on collections managed outside the parent payload.
    /// </summary>
    public IReadOnlyList<SchemaProperty>? Children { get; internal set; }
}

/// <summary>
///     Schema describing an entity property.
/// </summary>
/// <typeparam name="T">The model of the entity</typeparam>
public class SchemaProperty<T> : SchemaProperty
    where T : class, IEntity
{
    private const string CollectionType = "Collection";

    private static readonly IReadOnlyList<SchemaProperty<T>> Cached =
        typeof(T).GetProperties().Select(property => new SchemaProperty<T>(property)).ToArray();

    // Full serialized schema: property-backed entries plus one synthetic Collection entry per
    // Cascade pivot targeting T. Lazy so resolving child schemas never runs during type init.
    private static readonly Lazy<IReadOnlyList<SchemaProperty<T>>> Schema = new(BuildSchema);

    private readonly Func<T, object?> _getter;
    private readonly Regex? _regex;
    private readonly Type? _childType;
    private readonly bool _embedChildren;

    public SchemaProperty(PropertyInfo propertyInfo)
    {
        Name = propertyInfo.Name;
        Path = AttributeAnalyzer<T>.GetPath(propertyInfo);
        IsReadOnly = AttributeAnalyzer<T>.IsReadOnly(propertyInfo);
        IsSecret = AttributeAnalyzer<T>.IsSecret(propertyInfo);
        IsRequired = AttributeAnalyzer<T>.IsRequired(propertyInfo);
        IsUnique = AttributeAnalyzer<T>.IsUnique(propertyInfo);
        IsTemplateTarget = AttributeAnalyzer<T>.IsTemplateTarget(propertyInfo);
        IsTemplateSource = AttributeAnalyzer<T>.IsTemplateSource(propertyInfo);
        MaxLength = AttributeAnalyzer<T>.MaxLength(propertyInfo);
        IsIdentity = AttributeAnalyzer<T>.IsIdentity(propertyInfo);
        IsForeignKey = AttributeAnalyzer<T>.IsForeignKey(propertyInfo);
        _regex = AttributeAnalyzer<T>.GetRegex(propertyInfo);
        RegexValidation = _regex?.ToString();
        OneOfValues = AttributeAnalyzer<T>.GetOneOf(propertyInfo);
        _getter = BuildGetter(propertyInfo);

        var underlying = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;
        var elementType = GetEntityCollectionElement(underlying);
        if (elementType is not null)
        {
            Type = CollectionType;
            EnumValues = null;
            _childType = elementType;
            _embedChildren = AttributeAnalyzer<T>.HasChildSchema(propertyInfo);
        }
        else if (underlying.IsEnum)
        {
            Type = "Enum";
            EnumValues = Enum.GetNames(underlying);
        }
        else
        {
            Type = underlying.Name;
            EnumValues = null;
        }
    }

    private SchemaProperty(string virtualPath, Type childType)
    {
        var accessor = virtualPath.TrimStart('/');
        Name = char.ToUpperInvariant(accessor[0]) + accessor[1..];
        Path = accessor;
        Type = CollectionType;
        _childType = childType;
        _embedChildren = true;
        _getter = static _ => null;
    }

    /// <summary>
    ///     Get a schema of the entity describing its properties, including its inline-edited child
    ///     collections (see <see cref="SchemaProperty.Children" />). Built once per closed type and shared.
    /// </summary>
    public static IReadOnlyList<SchemaProperty<T>> Get() => Schema.Value;

    /// <summary>
    ///     Validate an <see cref="Entity" /> payload for an EFCore creation.
    /// </summary>
    public static void Validate(T entity)
    {
        foreach (var property in Cached)
            property.ValidatePath(property._getter(entity), property.Name);
    }

    /// <summary>
    ///     Validate a path for an EFCore creation.
    /// </summary>
    public void ValidatePath(object? value, string path)
    {
        if (value is null)
            return;
        if (
            (IsIdentity || IsForeignKey)
            && value.ToString() is "00000000-0000-0000-0000-000000000000" or "0"
        )
            return;
        if (path is "CreatedAt" or "UpdatedAt" && (DateTime)value == DateTime.MinValue)
            return;
        if (IsIdentity)
            throw new EntityValidationException($"{path}: This field is read-only.");
        if (IsRequired && Type == "String" && string.IsNullOrWhiteSpace(value.ToString()))
            throw new EntityValidationException($"{path}: This field is required and cannot be empty.");
        if (_regex is not null && !_regex.IsMatch(value.ToString() ?? ""))
            throw new EntityValidationException($"{path}: This value does not meet the requirements.");
        if (OneOfValues is not null && !OneOfValues.Contains(value.ToString() ?? string.Empty))
            throw new EntityValidationException($"{path}: Value must be one of: {string.Join(", ", OneOfValues)}.");
    }

    /// <summary>
    ///     Validate a path for an EFCore mutation.
    /// </summary>
    public void ValidatePathMutation(object? value, string path)
    {
        ValidatePath(value, path);
        // IsSecret act as read-only to avoid advertising which fields are secrets.
        if (IsIdentity || IsReadOnly || IsSecret)
            throw new EntityValidationException($"{path}: This field is read-only.");
    }

    private static IReadOnlyList<SchemaProperty<T>> BuildSchema()
    {
        foreach (var property in Cached)
            if (property is { _embedChildren: true, _childType: not null })
                property.Children = OwnPropertiesOf(property._childType);

        var pivots = ScanCascadePivots();
        return pivots.Count == 0 ? Cached : [.. Cached, .. pivots];
    }

    // Pivots live in the same assembly as their parent entity; the scan is bounded on purpose.
    private static List<SchemaProperty<T>> ScanCascadePivots()
    {
        var entries = new List<SchemaProperty<T>>();
        foreach (var type in PivotReflection.SafeGetTypes(typeof(T).Assembly))
        {
            if (type is not { IsClass: true, IsAbstract: false })
                continue;
            var resolution = type.GetCustomAttribute<PivotResolutionAttribute>();
            if (resolution is null || resolution.Mode != Ownership.Cascade)
                continue;
            if (!PivotReflection.TryExtractArgs(type, out var parentType, out var childType) || parentType != typeof(T))
                continue;
            entries.Add(new SchemaProperty<T>(resolution.VirtualPath, childType) { Children = OwnPropertiesOf(childType) });
        }

        return entries.OrderBy(e => e.Name, StringComparer.Ordinal).ToList();
    }

    // Embedded child schemas stay one level deep: the child's property-backed entries, without
    // resolving the child's own pivots.
    private static IReadOnlyList<SchemaProperty> OwnPropertiesOf(Type entityType)
    {
        var field = typeof(SchemaProperty<>)
                        .MakeGenericType(entityType)
                        .GetField(nameof(Cached), BindingFlags.NonPublic | BindingFlags.Static)
                    ?? throw new InvalidOperationException($"SchemaProperty<{entityType.Name}>.Cached not found.");
        return (IReadOnlyList<SchemaProperty>)field.GetValue(null)!;
    }

    private static Type? GetEntityCollectionElement(Type propertyType)
    {
        if (!propertyType.IsGenericType || propertyType.GenericTypeArguments.Length != 1)
            return null;
        var element = propertyType.GenericTypeArguments[0];
        if (!typeof(IEntity).IsAssignableFrom(element))
            return null;
        return typeof(IEnumerable<>).MakeGenericType(element).IsAssignableFrom(propertyType) ? element : null;
    }

    // Compiled accessor (boxed) so Validate reads values without a per-call GetValue reflection hit.
    private static Func<T, object?> BuildGetter(PropertyInfo info)
    {
        if (!info.CanRead) return static _ => null;
        var param = Expression.Parameter(typeof(T), "e");
        var body = Expression.Convert(Expression.Property(param, info), typeof(object));
        return Expression.Lambda<Func<T, object?>>(body, param).Compile();
    }
}
