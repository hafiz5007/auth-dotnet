using AuthReference.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthReference.Infrastructure.Persistence.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id");

        builder.Property(u => u.Email)
            .HasColumnName("email")
            .IsRequired()
            .HasMaxLength(320);
        builder.HasIndex(u => u.Email).IsUnique().HasDatabaseName("ux_users_email");

        builder.Property(u => u.EmailVerified).HasColumnName("email_verified");

        builder.Property(u => u.PasswordHash)
            .HasColumnName("password_hash")
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(u => u.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(120);

        builder.Property(u => u.Roles)
            .HasColumnName("roles")
            .IsRequired()
            .HasMaxLength(500)
            .HasDefaultValue("user");

        builder.Property(u => u.TokenVersion)
            .HasColumnName("token_version")
            .IsRequired()
            .HasDefaultValue(1)
            // Optimistic concurrency: a concurrent bump of TokenVersion should
            // cause the losing update to fail. Postgres xmin gives us that.
            .IsConcurrencyToken();

        builder.Property(u => u.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(u => u.LastLoginAtUtc)
            .HasColumnName("last_login_at_utc");
    }
}
