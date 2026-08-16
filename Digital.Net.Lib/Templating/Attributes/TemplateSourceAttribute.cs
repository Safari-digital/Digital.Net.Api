namespace Digital.Net.Lib.Templating.Attributes;

/// <summary>
///     Marks a string property as an interpolation source: it is exposed as a {{ entity.field }} variable.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class TemplateSourceAttribute : Attribute;
