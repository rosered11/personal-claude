using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OMS.Domain.Aggregates.OrderAggregate;

namespace OMS.Infrastructure.Persistence.Configurations;

public class OrderHoldConfiguration : IEntityTypeConfiguration<OrderHold>
{
    public void Configure(EntityTypeBuilder<OrderHold> builder)
    {
        builder.ToTable("order_holds");

        builder.HasKey(h => h.HoldId);
        builder.Property(h => h.HoldId).HasColumnName("hold_id");
        builder.Property(h => h.OrderId).HasColumnName("order_id");
        builder.Property(h => h.HoldReason).HasColumnName("hold_reason").HasMaxLength(500);
        builder.Property(h => h.HeldAt).HasColumnName("held_at");
        builder.Property(h => h.ReleasedAt).HasColumnName("released_at");
        builder.Property(h => h.HeldBy).HasColumnName("held_by").HasMaxLength(200);
        builder.Property(h => h.ReleasedBy).HasColumnName("released_by").HasMaxLength(200);
    }
}
