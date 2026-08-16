using System.ComponentModel.DataAnnotations.Schema;
using Digital.Net.Lib.Abstractions;

namespace Digital.Net.Lib.Entities.Models;

/// <summary>
///     Base class for join tables linking <typeparamref name="TParent" /> to <typeparamref name="TChild" />.
///     All pivots share the same shape: (ParentId, ChildId) composite key plus an <see cref="Order" />
///     column managed by the framework.
/// </summary>
public abstract class Pivot<TParent, TChild> : IEntity
    where TParent : Entity
    where TChild : Entity
{
    [Column("ParentId")]
    public Guid ParentId { get; set; }

    [Column("ChildId")]
    public Guid ChildId { get; set; }

    [Column("Order")]
    public int Order { get; set; }

    public virtual TParent Parent { get; set; } = null!;
    public virtual TChild Child { get; set; } = null!;
}