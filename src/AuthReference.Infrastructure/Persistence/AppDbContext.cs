using AuthReference.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthReference.Infrastructure.Persistence;

/// <summary>
/// The one and only <see cref="DbContext"/> for the auth service. Owns the
/// three domain tables plus OpenIddict's own entity sets (added by
/// <c>options.UseOpenIddict()</c> in <see cref="DependencyInjection"/>).
/// </summary>
public class AppDbContext : DbContext
{
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuthAuditEvent> AuditEvents => Set<AuthAuditEvent>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply every IEntityTypeConfiguration in this assembly.
        // Configurations live in Persistence/Configurations/ — one file per entity.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
