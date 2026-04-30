using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OMS.Domain.Aggregates.ReturnAggregate;
using OMS.Domain.Enums;

namespace OMS.Infrastructure.Persistence.Configurations;

public class ReturnItemConfiguration : IEntityTypeConfiguration<ReturnItem>
{
    public void Configure(EntityTypeBuilder<ReturnItem> builder)
    {
        builder.ToTable("return_items");

        builder.HasKey(i => i.ReturnItemId);
        builder.Property(i => i.ReturnItemId).HasColumnName("return_item_id");
        builder.Property(i => i.ReturnId).HasColumnName("return_id");
        builder.Property(i => i.OrderLineId).HasColumnName("order_line_id");
        builder.Property(i => i.Sku).HasColumnName("sku").HasMaxLength(100);
        builder.Property(i => i.ProductName).HasColumnName("product_name").HasMaxLength(300);
        builder.Property(i => i.Barcode).HasColumnName("barcode").HasMaxLength(100);
        builder.Property(i => i.Quantity).HasColumnName("quantity").HasPrecision(18, 4);
        builder.Property(i => i.UnitOfMeasure)
            .HasColumnName("unit_of_measure")
            .HasConversion<string>()
            .HasMaxLength(50);
        builder.Property(i => i.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 4);
        builder.Property(i => i.Currency).HasColumnName("currency").HasMaxLength(10);
        builder.Property(i => i.ItemReason).HasColumnName("item_reason").HasMaxLength(500);
        var nullableItemConditionConverter = new ValueConverter<ItemCondition?, string?>(
            v => v.HasValue ? v.Value.ToString() : null,
            v => v != null ? Enum.Parse<ItemCondition>(v) : (ItemCondition?)null);

        builder.Property(i => i.Condition)
            .HasColumnName("condition")
            .HasConversion(nullableItemConditionConverter)
            .HasMaxLength(50);
        builder.Property(i => i.PutAwayStatus).HasColumnName("put_away_status").HasMaxLength(50);
        builder.Property(i => i.AssignedSloc).HasColumnName("assigned_sloc").HasMaxLength(100);
        builder.Property(i => i.PaymentMethod)
            .HasColumnName("payment_method")
            .HasConversion<string>()
            .HasMaxLength(50);
        builder.Property(i => i.InspectedAt).HasColumnName("inspected_at");
        builder.Property(i => i.PutAwayAt).HasColumnName("put_away_at");
        builder.Property(i => i.CreatedAt).HasColumnName("created_at");
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at");
    }
}
