using Digital.Net.Core.Entities.Models.ApiKeys;
using Digital.Net.Core.Entities.Models.Auth;
using Digital.Net.Core.Entities.Models.Avatars;
using Digital.Net.Core.Entities.Models.Documents;
using Digital.Net.Core.Entities.Models.Sessions;
using Digital.Net.Core.Entities.Models.Users;
using Digital.Net.Lib.Entities.Context;
using Digital.Net.Lib.Entities.Interceptors;
using Digital.Net.Lib.Entities.Models;
using Digital.Net.Lib.Entities.Mutations;
using Microsoft.EntityFrameworkCore;

namespace Digital.Net.Core.Entities.Context;

public class DigitalContext(DbContextOptions<DigitalContext> options) : DbContext(options), IDigitalNetContext
{
    public const string Schema = "digital_net";
    static string IDigitalNetContext.Schema => Schema;

    public DbSet<EntityMutation> EntityMutations { get; init; }
    public DbSet<ApiKey> ApiKeys { get; init; }
    public DbSet<AuthEvent> AuthEvents { get; init; }
    public DbSet<Avatar> Avatars { get; init; }
    public DbSet<Document> Documents { get; init; }
    public DbSet<Session> Sessions { get; init; }
    public DbSet<User> Users { get; init; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => 
        optionsBuilder.AddInterceptors(
            new TimestampInterceptor()
        );

    protected override void OnModelCreating(ModelBuilder builder) =>
        builder
            .HasDefaultSchema(Schema)
            .BuildUser()
            .ConfigurePivots();
}
