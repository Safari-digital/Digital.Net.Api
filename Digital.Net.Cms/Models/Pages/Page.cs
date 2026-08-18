using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Digital.Net.Lib.Entities.Attributes;
using Digital.Net.Lib.Templating.Attributes;
using Digital.Net.Lib.Entities.Models;
using Digital.Net.Lib.String;
using Microsoft.EntityFrameworkCore;

namespace Digital.Net.Cms.Models.Pages;

[Table("Page")]
[Index(nameof(Path), IsUnique = true)]
public class Page : Entity
{
    [Column("Path")]
    [Required]
    [MaxLength(2068)]
    [Sortable]
    [RegexValidation(RegularExpressions.PagePathPattern)]
    public required string Path { get; set; }

    [Column("Published")]
    [Sortable]
    public bool Published { get; set; }

    [Column("PublishedAt")]
    [Sortable]
    public DateTime? PublishedAt { get; set; }

    [Column("Indexed")]
    [Sortable]
    public bool Indexed { get; set; } = true;

    [Column("Title")]
    [TemplateTarget]
    [MaxLength(256)]
    [Sortable]
    public string? Title { get; set; }

    [Column("Description")]
    [TemplateTarget]
    [MaxLength(512)]
    public string? Description { get; set; }

    [Column("JsonLd")]
    [TemplateTarget]
    [MaxLength(65535)]
    public string? JsonLd { get; set; }

    [Column("Redirect")]
    [MaxLength(2068)]
    public string? Redirect { get; set; }
}
