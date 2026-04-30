using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OMS.Domain.Aggregates.PurchaseOrderAggregate;

namespace OMS.Infrastructure.Persistence.Configurations;

public class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> builder)
    {
        builder.ToTable("purchase_order_lines");

        builder.HasKey(l => l.LineId);
        builder.Property(l => l.LineId).HasColumnName("line_id");
        builder.Property(l => l.PurchaseOrderId).HasColumnName("purchase_order_id");
        builder.Property(l => l.Sku).HasColumnName("sku").HasMaxLength(100).IsRequired();
        builder.Property(l => l.ProductName).HasColumnName("product_name").HasMaxLength(500).IsRequired();
        builder.Property(l => l.OrderedQty).HasColumnName("ordered_qty").HasPrecision(18, 4);
        builder.Property(l => l.ReceivedQty).HasColumnName("received_qty").HasPrecision(18, 4);
        builder.Property(l => l.UnitOfMeasure)
            .HasColumnName("unit_of_measure")
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.Property(l => l.Condition)
            .HasColumnName("condition")
            .HasConversion<string>()
            .HasMaxLength(50);
        builder.Property(l => l.CreatedAt).HasColumnName("created_at");
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at");
    }
}
