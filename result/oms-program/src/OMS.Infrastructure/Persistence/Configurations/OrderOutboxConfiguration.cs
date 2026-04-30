using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OMS.Infrastructure.Outbox;

namespace OMS.Infrastructure.Persistence.Configurations;

public class OrderOutboxConfiguration : IEntityTypeConfiguration<OrderOutbox>
{
    public void Configure(EntityTypeBuilder<OrderOutbox> builder)
    {
        builder.ToTable("order_outbox");

        builder.HasKey(o => o.OutboxId);
        builder.Property(o => o.OutboxId).HasColumnName("outbox_id");
        builder.Property(o => o.OrderId).HasColumnName("order_id");
        builder.Property(o => o.EventType).HasColumnName("event_type").HasMaxLength(200).IsRequired();
        builder.Property(o => o.TargetSystem).HasColumnName("target_system").HasMaxLength(50).IsRequired();
        builder.Property(o => o.EventPayload).HasColumnName("event_payload").HasColumnType("jsonb").IsRequired();
        builder.Property(o => o.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50);
        builder.Property(o => o.RetryCount).HasColumnName("retry_count");
        builder.Property(o => o.NextRetryAt).HasColumnName("next_retry_at");
        builder.Property(o => o.CreatedAt).HasColumnName("created_at");
        builder.Property(o => o.PublishedAt).HasColumnName("published_at");

        builder.HasIndex(o => new { o.Status, o.NextRetryAt });
    }
}
