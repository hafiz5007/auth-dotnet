using AuthReference.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthReference.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");

        builder.Property(t => t.UserId)
            .HasColumnName("user_id")
            .IsRequired();
        builder.HasIndex(t => t.UserId).HasDatabaseName("ix_refresh_tokens_user_id");

        builder.Property(t => t.TokenHash)
            .HasColumnName("token_hash")
            .IsRequired()
            .HasMaxLength(64);          // SHA256 hex
        builder.HasIndex(t => t.TokenHash).IsUnique().HasDatabaseName("ux_refresh_tokens_hash");

        builder.Property(t => t.IssuedAtUtc)
            .HasColumnName("issued_at_utc")
            .IsRequired();

        builder.Property(t => t.ExpiresAtUtc)
            .HasColumnName("expires_at_utc")
            .IsRequired();
        builder.HasIndex(t => t.ExpiresAtUtc).HasDatabaseName("ix_refresh_tokens_expires_at");

        builder.Property(t => t.RevokedAtUtc)
            .HasColumnName("revoked_at_utc");

        builder.Property(t => t.ReplacedById)
            .HasColumnName("replaced_by_id");

        builder.Property(t => t.RevokeReason)
            .HasColumnName("revoke_reason")
            .HasMaxLength(200);
    }
}
