using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OMS.Domain.Aggregates.OrderAggregate;

namespace OMS.Infrastructure.Persistence.Configurations;

public class OrderPackageConfiguration : IEntityTypeConfiguration<OrderPackage>
{
    public void Configure(EntityTypeBuilder<OrderPackage> builder)
    {
        builder.ToTable("order_packages");

        builder.HasKey(p => p.PackageId);
        builder.Property(p => p.PackageId).HasColumnName("package_id");
        builder.Property(p => p.OrderId).HasColumnName("order_id");
        builder.Property(p => p.TrackingId).HasColumnName("tracking_id").HasMaxLength(200).IsRequired();
        builder.HasIndex(p => p.TrackingId).IsUnique();
        builder.Property(p => p.CarrierPackageId).HasColumnName("carrier_package_id").HasMaxLength(200);
        builder.Property(p => p.ThirdPartyLogistic).HasColumnName("third_party_logistic").HasMaxLength(200);
        builder.Property(p => p.VehicleType)
            .HasColumnName("vehicle_type")
            .HasConversion<string>()
            .HasMaxLength(50);
        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50);
        builder.Property(p => p.PackageWeight).HasColumnName("package_weight").HasPrecision(18, 4);
        builder.Property(p => p.DeliveryNoteNumber).HasColumnName("delivery_note_number").HasMaxLength(100);
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");

        builder.HasMany<OrderPackageLine>("_packageLines")
            .WithOne()
            .HasForeignKey(pl => pl.PackageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation("_packageLines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class OrderPackageLineConfiguration : IEntityTypeConfiguration<OrderPackageLine>
{
    public void Configure(EntityTypeBuilder<OrderPackageLine> builder)
    {
        builder.ToTable("order_package_lines");

        builder.HasKey(pl => pl.Id);
        builder.Property(pl => pl.Id).HasColumnName("id");
        builder.Property(pl => pl.PackageId).HasColumnName("package_id");
        builder.Property(pl => pl.OrderLineId).HasColumnName("order_line_id");
    }
}
