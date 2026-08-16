namespace Digital.Net.Lib.Templating.Attributes;

/// <summary>
///     Marks the foreign key of an interpolation source towards the entity hosting its interpolation: the
///     referenced host is the one whose <see cref="TemplateTargetAttribute" /> fields this entity hydrates.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class TemplateHostAttribute : Attribute;