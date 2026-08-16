namespace Digital.Net.Lib.Templating.Models;

/// <summary>
///     Describes a single template variable exposed by a source entity type.
/// </summary>
/// <param name="Token">The full placeholder usable in a template, e.g. <c>{{ entity.field }}</c>.</param>
/// <param name="Source">The lowercase alias of the source entity type, e.g. <c>entity</c>.</param>
/// <param name="Field">The property name on the source entity, e.g. <c>field</c>.</param>
public record TemplateVariableDescriptor(string Token, string Source, string Field);
