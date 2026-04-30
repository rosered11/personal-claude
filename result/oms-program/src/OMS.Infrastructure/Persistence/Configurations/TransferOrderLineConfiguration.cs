using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OMS.Domain.Aggregates.TransferOrderAggregate;

namespace OMS.Infrastructure.Persistence.Configurations;

public class TransferOrderLineConfiguration : IEntityTypeConfiguration<TransferOrderLine>
{
    public void Configure(EntityTypeBuilder<TransferOrderLine> builder)
    {
        builder.ToTable("transfer_order_lines");

        builder.HasKey(l => l.LineId);
        builder.Property(l => l.LineId).HasColumnName("line_id");
        builder.Property(l => l.TransferOrderId).HasColumnName("transfer_order_id");
        builder.Property(l => l.Sku).HasColumnName("sku").HasMaxLength(100).IsRequired();
        builder.Property(l => l.ProductName).HasColumnName("product_name").HasMaxLength(500).IsRequired();
        builder.Property(l => l.RequestedQty).HasColumnName("requested_qty").HasPrecision(18, 4);
        builder.Property(l => l.TransferredQty).HasColumnName("transferred_qty").HasPrecision(18, 4);
        builder.Property(l => l.UnitOfMeasure)
            .HasColumnName("unit_of_measure")
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.Property(l => l.CreatedAt).HasColumnName("created_at");
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at");
    }
}
