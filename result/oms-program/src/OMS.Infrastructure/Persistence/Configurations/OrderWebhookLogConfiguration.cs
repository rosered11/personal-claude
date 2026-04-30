using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OMS.Infrastructure.Inbound;

namespace OMS.Infrastructure.Persistence.Configurations;

public class OrderWebhookLogConfiguration : IEntityTypeConfiguration<OrderWebhookLog>
{
    public void Configure(EntityTypeBuilder<OrderWebhookLog> builder)
    {
        builder.ToTable("order_webhook_logs");
        builder.HasKey(e => e.WebhookLogId);
        builder.Property(e => e.WebhookLogId).HasColumnName("webhook_log_id");
        builder.Property(e => e.OrderId).HasColumnName("order_id");
        builder.Property(e => e.SourceSystem).HasColumnName("source_system").HasMaxLength(50).IsRequired();
        builder.Property(e => e.EventType).HasColumnName("event_type").HasMaxLength(100).IsRequired();
        builder.Property(e => e.Detail).HasColumnName("detail").HasMaxLength(500);
        builder.Property(e => e.ReceivedAt).HasColumnName("received_at");
        builder.HasIndex(e => e.OrderId);
        builder.HasIndex(e => e.ReceivedAt);
    }
}
