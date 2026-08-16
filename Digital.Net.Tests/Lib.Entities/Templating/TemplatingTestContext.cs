using Digital.Net.Lib.Entities.Interceptors;
using Microsoft.EntityFrameworkCore;

namespace Digital.Net.Tests.Lib.Entities.Templating;

public class TemplatingTestContext(DbContextOptions<TemplatingTestContext> options) : DbContext(options)
{
    public DbSet<TemplatingTestSource> Sources { get; init; }
    public DbSet<TemplatingTestOtherSource> OtherSources { get; init; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.AddInterceptors(new TimestampInterceptor());

    protected override void OnModelCreating(ModelBuilder builder) => builder.HasDefaultSchema("templating_test");
}