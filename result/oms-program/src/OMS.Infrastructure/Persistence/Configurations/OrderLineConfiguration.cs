using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OMS.Domain.Aggregates.OrderAggregate;

namespace OMS.Infrastructure.Persistence.Configurations;

public class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> builder)
    {
        builder.ToTable("order_lines");

        builder.HasKey(l => l.OrderLineId);
        builder.Property(l => l.OrderLineId).HasColumnName("order_line_id");
        builder.Property(l => l.OrderId).HasColumnName("order_id");

        builder.Property(l => l.Sku).HasColumnName("sku").HasMaxLength(100).IsRequired();
        builder.Property(l => l.ProductName).HasColumnName("product_name").HasMaxLength(300).IsRequired();
        builder.Property(l => l.Barcode).HasColumnName("barcode").HasMaxLength(100);
        builder.Property(l => l.RequestedAmount).HasColumnName("requested_amount").HasPrecision(18, 4);
        builder.Property(l => l.PickedAmount).HasColumnName("picked_amount").HasPrecision(18, 4);
        builder.Property(l => l.UnitOfMeasure)
            .HasColumnName("unit_of_measure")
            .HasConversion<string>()
            .HasMaxLength(50);
        builder.Property(l => l.OriginalUnitPrice).HasColumnName("original_unit_price").HasPrecision(18, 4);
        builder.Property(l => l.Currency).HasColumnName("currency").HasMaxLength(10);
        builder.Property(l => l.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50);
        builder.Property(l => l.IsSubstitute).HasColumnName("is_substitute");
        builder.Property(l => l.CreatedAt).HasColumnName("created_at");
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at");
    }
}
