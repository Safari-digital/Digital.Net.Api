using Digital.Net.Cms.Models;
using Digital.Net.Cms.Models.Forms;
using Digital.Net.Cms.Models.Medias;
using Digital.Net.Cms.Models.Pages;
using Digital.Net.Lib.Entities.Context;
using Digital.Net.Lib.Entities.Interceptors;
using Digital.Net.Lib.Entities.Models;
using Digital.Net.Lib.Entities.Mutations;
using Microsoft.EntityFrameworkCore;

namespace Digital.Net.Cms.Context;

public class CmsContext(DbContextOptions<CmsContext> options) : DbContext(options), IDigitalNetContext
{
    public const string Schema = "digital_net_cms";
    static string IDigitalNetContext.Schema => Schema;

    public DbSet<Page> Pages { get; init; }
    public DbSet<Sheet> Sheets { get; init; }
    public DbSet<PageSheet> PageSheets { get; init; }
    public DbSet<OpenGraphEntry> OpenGraphEntries { get; init; }
    public DbSet<PageOpenGraph> PageOpenGraphs { get; init; }
    public DbSet<Media> Media { get; init; }
    public DbSet<MediaVariant> MediaVariants { get; init; }
    public DbSet<Form> Forms { get; init; }
    public DbSet<FormField> FormFields { get; init; }
    public DbSet<FormSubmission> FormSubmissions { get; init; }
    public DbSet<EntityMutation> EntityMutations { get; init; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.AddInterceptors(new TimestampInterceptor());

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema(Schema);
        builder.BuildMediaVariant();
        builder.BuildFormField();
        builder.BuildFormSubmission();
        builder.ConfigurePivots();
    }
}
