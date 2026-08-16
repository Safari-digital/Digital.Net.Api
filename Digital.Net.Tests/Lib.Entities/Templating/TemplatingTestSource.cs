using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Digital.Net.Lib.Entities.Models;
using Digital.Net.Lib.Templating.Attributes;

namespace Digital.Net.Tests.Lib.Entities.Templating;

[Table("TestSource")]
public class TemplatingTestSource : Entity
{
    [Column("Title")]
    [Required]
    [TemplateSource]
    [MaxLength(64)]
    public required string Title { get; set; }

    [Column("HostId")]
    [TemplateHost]
    public Guid? HostId { get; set; }
}

[Table("TestOtherSource")]
public class TemplatingTestOtherSource : Entity
{
    [Column("Label")]
    [Required]
    [TemplateSource]
    [MaxLength(64)]
    public required string Label { get; set; }

    [Column("HostId")]
    [TemplateHost]
    public Guid? HostId { get; set; }
}