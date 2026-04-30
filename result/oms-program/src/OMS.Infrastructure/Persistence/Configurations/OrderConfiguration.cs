using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OMS.Domain.Aggregates.OrderAggregate;
using OMS.Domain.Enums;
using OMS.Domain.ValueObjects;

namespace OMS.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");

        builder.HasKey(o => o.OrderId);
        builder.Property(o => o.OrderId).HasColumnName("order_id");

        builder.Property(o => o.OrderNumber)
            .HasColumnName("order_number")
            .HasMaxLength(100)
            .IsRequired();
        builder.HasIndex(o => o.OrderNumber).IsUnique();

        builder.Property(o => o.SourceOrderId)
            .HasColumnName("source_order_id")
            .HasMaxLength(200);
        builder.HasIndex(o => o.SourceOrderId).IsUnique().HasFilter("source_order_id IS NOT NULL");

        builder.Property(o => o.ChannelType)
            .HasColumnName("channel_type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(o => o.BusinessUnit)
            .HasColumnName("business_unit")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(o => o.StoreId).HasColumnName("store_id");

        builder.Property(o => o.OrderDate).HasColumnName("order_date");

        builder.Property(o => o.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        var nullableOrderStatusConverter = new ValueConverter<OrderStatus?, string?>(
            v => v.HasValue ? v.Value.ToString() : null,
            v => v != null ? Enum.Parse<OrderStatus>(v) : (OrderStatus?)null);

        builder.Property(o => o.PreHoldStatus)
            .HasColumnName("pre_hold_status")
            .HasConversion(nullableOrderStatusConverter)
            .HasMaxLength(50);

        builder.Property(o => o.HoldReason)
            .HasColumnName("hold_reason")
            .HasMaxLength(500);

        builder.Property(o => o.FulfillmentType)
            .HasColumnName("fulfillment_type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(o => o.PaymentMethod)
            .HasColumnName("payment_method")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(o => o.SubstitutionFlag).HasColumnName("substitution_flag");
        builder.Property(o => o.PosRecalcPending).HasColumnName("pos_recalc_pending");

        builder.Property(o => o.CreatedAt).HasColumnName("created_at");
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at");
        builder.Property(o => o.CreatedBy).HasColumnName("created_by").HasMaxLength(200);
        builder.Property(o => o.UpdatedBy).HasColumnName("updated_by").HasMaxLength(200);

        builder.OwnsOne<DeliverySlot>("_deliverySlot", slot =>
        {
            slot.ToTable("delivery_slots");
            slot.WithOwner().HasForeignKey("order_id");
            slot.Property(s => s.SlotId).HasColumnName("slot_id");
            slot.Property(s => s.StoreId).HasColumnName("store_id");
            slot.Property(s => s.ScheduledStart).HasColumnName("scheduled_start");
            slot.Property(s => s.ScheduledEnd).HasColumnName("scheduled_end");
        });

        builder.HasMany<OrderLine>("_orderLines")
            .WithOne()
            .HasForeignKey(l => l.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation("_orderLines").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany<OrderPackage>("_packages")
            .WithOne()
            .HasForeignKey(p => p.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation("_packages").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany<OrderHold>("_holds")
            .WithOne()
            .HasForeignKey(h => h.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation("_holds").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany<OrderStatusHistory>("_statusHistory")
            .WithOne()
            .HasForeignKey(h => h.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation("_statusHistory").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
