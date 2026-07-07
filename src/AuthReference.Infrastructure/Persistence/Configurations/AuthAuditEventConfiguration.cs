using AuthReference.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthReference.Infrastructure.Persistence.Configurations;

public class AuthAuditEventConfiguration : IEntityTypeConfiguration<AuthAuditEvent>
{
    public void Configure(EntityTypeBuilder<AuthAuditEvent> builder)
    {
        builder.ToTable("auth_audit_events");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.UserId).HasColumnName("user_id");
        builder.HasIndex(e => e.UserId).HasDatabaseName("ix_audit_user_id");

        builder.Property(e => e.EventType)
            .HasColumnName("event_type")
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(e => e.Detail)
            .HasColumnName("detail")
            .HasMaxLength(500);

        builder.Property(e => e.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(64);

        builder.Property(e => e.UserAgent)
            .HasColumnName("user_agent")
            .HasMaxLength(500);

        builder.Property(e => e.CorrelationId)
            .HasColumnName("correlation_id")
            .HasMaxLength(64);

        builder.Property(e => e.OccurredAtUtc)
            .HasColumnName("occurred_at_utc")
            .IsRequired();

        // Retention worker prunes by occurred_at_utc — needs an index.
        builder.HasIndex(e => e.OccurredAtUtc).HasDatabaseName("ix_audit_occurred_at");
    }
}
