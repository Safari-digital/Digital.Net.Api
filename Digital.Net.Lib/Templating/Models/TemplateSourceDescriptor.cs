namespace Digital.Net.Lib.Templating.Models;

/// <summary>
///     Describes an interpolation source.
/// </summary>
/// <param name="SourceType">The entity feeding the tokens.</param>
/// <param name="ForeignKey">Name of the property holding the id of the hosting entity.</param>
public sealed record TemplateSourceDescriptor(Type SourceType, string ForeignKey);