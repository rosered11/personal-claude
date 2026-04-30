using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OMS.Domain.Aggregates.PurchaseOrderAggregate;

namespace OMS.Infrastructure.Persistence.Configurations;

public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("purchase_orders");

        builder.HasKey(p => p.PurchaseOrderId);
        builder.Property(p => p.PurchaseOrderId).HasColumnName("purchase_order_id");
        builder.Property(p => p.PoNumber).HasColumnName("po_number").HasMaxLength(100).IsRequired();
        builder.HasIndex(p => p.PoNumber).IsUnique();
        builder.Property(p => p.SupplierId).HasColumnName("supplier_id").HasMaxLength(200).IsRequired();
        builder.Property(p => p.StoreId).HasColumnName("store_id");
        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50);
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.CreatedBy).HasColumnName("created_by").HasMaxLength(200);
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by").HasMaxLength(200);

        builder.HasMany<PurchaseOrderLine>("_lines")
            .WithOne()
            .HasForeignKey(l => l.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
