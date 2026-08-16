namespace Digital.Net.Lib.Templating.Attributes;

/// <summary>
///     Marks a string property as an interpolation target: placeholders like {{ entity.field }} held in its
///     value are hydrated.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class TemplateTargetAttribute : Attribute;
