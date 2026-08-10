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
public class TemplateHostAttribute : Attribute;
