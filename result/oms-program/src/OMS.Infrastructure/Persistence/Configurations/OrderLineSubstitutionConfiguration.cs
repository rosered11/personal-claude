using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OMS.Domain.Aggregates.OrderAggregate;

namespace OMS.Infrastructure.Persistence.Configurations;

public class OrderLineSubstitutionConfiguration : IEntityTypeConfiguration<OrderLineSubstitution>
{
    public void Configure(EntityTypeBuilder<OrderLineSubstitution> builder)
    {
        builder.ToTable("order_line_substitutions");
        builder.HasKey(s => s.SubstitutionId);
        builder.Property(s => s.SubstitutionId).HasColumnName("substitution_id");
        builder.Property(s => s.OrderLineId).HasColumnName("order_line_id").IsRequired();
        builder.Property(s => s.SubstituteOrderLineId).HasColumnName("substitute_order_line_id").IsRequired();
        builder.Property(s => s.SubstituteSku).HasColumnName("substitute_sku").HasMaxLength(100).IsRequired();
        builder.Property(s => s.SubstituteProductName).HasColumnName("substitute_product_name").HasMaxLength(500).IsRequired();
        builder.Property(s => s.SubstituteBarcode).HasColumnName("substitute_barcode").HasMaxLength(100).IsRequired();
        builder.Property(s => s.SubstituteUnitPrice).HasColumnName("substitute_unit_price").HasPrecision(18, 4).IsRequired();
        builder.Property(s => s.SubstitutedAmount).HasColumnName("substituted_amount").HasPrecision(18, 4).IsRequired();
        builder.Property(s => s.CustomerApproved).HasColumnName("customer_approved");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(s => s.OrderLineId).HasDatabaseName("ix_order_line_substitutions_order_line_id");
    }
}
