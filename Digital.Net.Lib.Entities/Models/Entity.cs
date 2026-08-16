using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Digital.Net.Lib.Abstractions;
using Digital.Net.Lib.Entities.Attributes;

namespace Digital.Net.Lib.Entities.Models;

/// <summary>
///     Base class for all DB entities used in Digital.Net projects.
/// </summary>
public abstract class Entity : EntityMeta, IEntity
{
    [Column("Id"), Key, DatabaseGenerated(DatabaseGeneratedOption.Identity), Sortable]
    public Guid Id { get; init; }

    /// <summary>
    ///     Strips EF Core proxy suffix so the dictionary key matches the logical type name (e.g. <c>"Article"</c>,
    ///     not <c>"ArticleProxy"</c>).
    /// </summary>
    public Type GetCanonicalType()
    {
        var type = GetType();
        return type.Name.EndsWith("Proxy") && type.BaseType is not null ? type.BaseType : type;
    }
}
