namespace Digital.Net.Cms.Templating;

/// <summary>
///     An interpolation source discovered by <c>AddTemplateSources</c>: an entity whose navigation carries
///     <see cref="Digital.Net.Lib.Entities.Attributes.TemplateHostAttribute" />. Registered as a singleton,
///     so the set of declared sources is enumerable through <c>IEnumerable&lt;TemplateSourceDescriptor&gt;</c>
///     without touching the database.
/// </summary>
/// <param name="SourceType">The entity feeding the tokens.</param>
/// <param name="ForeignKey">Name of the property holding the id of the hosting page.</param>
/// <param name="Discriminator">Transitional. Property telling apart sources sharing one host page.</param>
/// <param name="PublishedFlag">Transitional. Property gating publication.</param>
/// <param name="PublishedFlagIsBoolean">
///     True when <paramref name="PublishedFlag" /> is a <c>bool</c>, false when it is a <c>DateTime?</c>.
/// </param>
public sealed record TemplateSourceDescriptor(
    Type SourceType,
    string ForeignKey,
    string? Discriminator,
    string? PublishedFlag,
    bool PublishedFlagIsBoolean
);
