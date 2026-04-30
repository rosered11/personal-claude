using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OMS.Domain.Aggregates.OrderAggregate;

namespace OMS.Infrastructure.Persistence.Configurations;

public class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        builder.ToTable("order_status_history");

        builder.HasKey(h => h.HistoryId);
        builder.Property(h => h.HistoryId).HasColumnName("history_id").ValueGeneratedNever();
        builder.Property(h => h.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(h => h.FromStatus).HasColumnName("from_status").HasConversion<string>().HasMaxLength(50);
        builder.Property(h => h.ToStatus).HasColumnName("to_status").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(h => h.ChangedBy).HasColumnName("changed_by").HasMaxLength(100).IsRequired();
        builder.Property(h => h.Detail).HasColumnName("detail").HasMaxLength(500);
        builder.Property(h => h.ChangedAt).HasColumnName("changed_at").IsRequired();

        builder.HasIndex(h => h.OrderId).HasDatabaseName("idx_status_history_order_id");
        builder.HasIndex(h => h.ChangedAt).HasDatabaseName("idx_status_history_changed_at");
    }
}
