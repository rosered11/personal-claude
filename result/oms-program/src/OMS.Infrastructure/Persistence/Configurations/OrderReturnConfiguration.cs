using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OMS.Domain.Aggregates.ReturnAggregate;

namespace OMS.Infrastructure.Persistence.Configurations;

public class OrderReturnConfiguration : IEntityTypeConfiguration<OrderReturn>
{
    public void Configure(EntityTypeBuilder<OrderReturn> builder)
    {
        builder.ToTable("returns");

        builder.HasKey(r => r.ReturnId);
        builder.Property(r => r.ReturnId).HasColumnName("return_id");
        builder.Property(r => r.OrderId).HasColumnName("order_id");
        builder.Property(r => r.ReturnOrderNumber).HasColumnName("return_order_number").HasMaxLength(100).IsRequired();
        builder.HasIndex(r => r.ReturnOrderNumber).IsUnique();
        builder.Property(r => r.InvoiceId).HasColumnName("invoice_id").HasMaxLength(100);
        builder.Property(r => r.CreditNoteId).HasColumnName("credit_note_id").HasMaxLength(100);
        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50);
        builder.Property(r => r.GoodsReceiveNo).HasColumnName("goods_receive_no").HasMaxLength(100);
        builder.Property(r => r.ReturnReason).HasColumnName("return_reason").HasMaxLength(500);
        builder.Property(r => r.RequestedAt).HasColumnName("requested_at");
        builder.Property(r => r.PickupScheduledAt).HasColumnName("pickup_scheduled_at");
        builder.Property(r => r.PickedUpAt).HasColumnName("picked_up_at");
        builder.Property(r => r.ReceivedAt).HasColumnName("received_at");
        builder.Property(r => r.InspectedAt).HasColumnName("inspected_at");
        builder.Property(r => r.PutAwayAt).HasColumnName("put_away_at");
        builder.Property(r => r.RefundedAt).HasColumnName("refunded_at");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.CreatedBy).HasColumnName("created_by").HasMaxLength(200);
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by").HasMaxLength(200);

        builder.HasMany<ReturnItem>("_returnItems")
            .WithOne()
            .HasForeignKey(i => i.ReturnId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation("_returnItems").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany<ReturnPutAwayLog>("_putAwayLogs")
            .WithOne()
            .HasForeignKey(l => l.ReturnId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation("_putAwayLogs").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
