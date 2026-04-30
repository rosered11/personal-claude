using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OMS.Domain.Aggregates.ReturnAggregate;

namespace OMS.Infrastructure.Persistence.Configurations;

public class ReturnPutAwayLogConfiguration : IEntityTypeConfiguration<ReturnPutAwayLog>
{
    public void Configure(EntityTypeBuilder<ReturnPutAwayLog> builder)
    {
        builder.ToTable("return_put_away_logs");

        builder.HasKey(l => l.LogId);
        builder.Property(l => l.LogId).HasColumnName("log_id");
        builder.Property(l => l.ReturnId).HasColumnName("return_id");
        builder.Property(l => l.ReturnItemId).HasColumnName("return_item_id");
        builder.Property(l => l.Sku).HasColumnName("sku").HasMaxLength(100);
        builder.Property(l => l.AssignedSloc).HasColumnName("assigned_sloc").HasMaxLength(100);
        builder.Property(l => l.Condition)
            .HasColumnName("condition")
            .HasConversion<string>()
            .HasMaxLength(50);
        builder.Property(l => l.Quantity).HasColumnName("quantity").HasPrecision(18, 4);
        builder.Property(l => l.PerformedBy).HasColumnName("performed_by").HasMaxLength(200);
        builder.Property(l => l.PerformedAt).HasColumnName("performed_at");
    }
}
