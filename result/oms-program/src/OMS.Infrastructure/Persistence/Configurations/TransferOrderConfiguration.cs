using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OMS.Domain.Aggregates.TransferOrderAggregate;

namespace OMS.Infrastructure.Persistence.Configurations;

public class TransferOrderConfiguration : IEntityTypeConfiguration<TransferOrder>
{
    public void Configure(EntityTypeBuilder<TransferOrder> builder)
    {
        builder.ToTable("transfer_orders");

        builder.HasKey(t => t.TransferOrderId);
        builder.Property(t => t.TransferOrderId).HasColumnName("transfer_order_id");
        builder.Property(t => t.TransferNumber).HasColumnName("transfer_number").HasMaxLength(100).IsRequired();
        builder.HasIndex(t => t.TransferNumber).IsUnique();
        builder.Property(t => t.SourceStoreId).HasColumnName("source_store_id");
        builder.Property(t => t.DestStoreId).HasColumnName("dest_store_id");
        builder.Property(t => t.TrackingId).HasColumnName("tracking_id").HasMaxLength(200);
        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50);
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.Property(t => t.CreatedBy).HasColumnName("created_by").HasMaxLength(200);
        builder.Property(t => t.UpdatedBy).HasColumnName("updated_by").HasMaxLength(200);

        builder.HasMany<TransferOrderLine>("_lines")
            .WithOne()
            .HasForeignKey(l => l.TransferOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
