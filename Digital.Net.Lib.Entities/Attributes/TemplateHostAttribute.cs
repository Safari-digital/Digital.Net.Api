namespace Digital.Net.Lib.Entities.Attributes;

/// <summary>
///     Marks the foreign key of an interpolation source towards the page hosting its interpolation: the
///     referenced page is the one whose <see cref="TemplateTargetAttribute" /> fields this entity hydrates.
///     This declaration, and not an enum held by the library, answers "which entity feeds this page".
///     <para>
///         It goes on the key itself rather than on a navigation, so a source can host on a page held by
///         another context — which is the normal case once entities live in a client-owned schema.
///     </para>
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class TemplateHostAttribute : Attribute
{
    /// <summary>
    ///     Transitional. Name of the property telling apart several sources sharing a single host page.
    ///     Drop it once every source owns a dedicated page: resolution then reduces to the foreign key.
    /// </summary>
    public string? Discriminator { get; init; }

    /// <summary>
    ///     Transitional. Name of the property gating publication, either a <c>bool</c> (true publishes) or a
    ///     <c>DateTime?</c> (a value publishes). Drop it once the dedicated page carries the state.
    /// </summary>
    public string? PublishedFlag { get; init; }
}
