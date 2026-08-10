using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Digital.Net.Cms.Models.Pages;
using Digital.Net.Lib.Entities.Attributes;
using Digital.Net.Lib.Entities.Models;
using Digital.Net.Lib.String;
using Microsoft.EntityFrameworkCore;

namespace Digital.Net.Cms.Models.Articles;

[Table("Article")]
[Index(nameof(Slug), IsUnique = true)]
public class Article : Entity
{
    [Column("Title")]
    [Required]
    [TemplateSource]
    [MaxLength(256)]
    [Sortable]
    public required string Title { get; set; }

    [Column("Description")]
    [Required]
    [TemplateSource]
    [MaxLength(512)]
    public required string Description { get; set; }

    [Column("Content")]
    [Required]
    public required string Content { get; set; }

    [Column("Slug")]
    [Required]
    [TemplateSource]
    [MaxLength(256)]
    [Sortable]
    [RegexValidation(RegularExpressions.ArticleSlugPattern)]
    public required string Slug { get; set; }

    [Column("PublishedAt")]
    [Sortable]
    public DateTime? PublishedAt { get; set; }

    // Discriminator and PublishedFlag are transitional: N articles still share one template page, so the
    // slug tells them apart and PublishedAt gates publication. Both drop once every article owns a
    // dedicated page carrying its own Published state — resolution then reduces to the foreign key.
    [Column("PageId")]
    [ForeignKey("Page")]
    [TemplateHost(Discriminator = nameof(Slug), PublishedFlag = nameof(PublishedAt))]
    public Guid? PageId { get; set; }

    [ReadOnly]
    public virtual Page? Page { get; set; }

    public virtual ICollection<Tag> Tags { get; set; } = new List<Tag>();
}
