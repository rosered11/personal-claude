using Microsoft.EntityFrameworkCore;
using OMS.Domain.Aggregates.OrderAggregate;
using OMS.Domain.Aggregates.PurchaseOrderAggregate;
using OMS.Domain.Aggregates.ReturnAggregate;
using OMS.Domain.Aggregates.TransferOrderAggregate;
using OMS.Infrastructure.Inbound;
using OMS.Infrastructure.Outbox;

namespace OMS.Infrastructure.Persistence;

public class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLineSubstitution> OrderLineSubstitutions => Set<OrderLineSubstitution>();
    public DbSet<OrderReturn> Returns => Set<OrderReturn>();
    public DbSet<OrderOutbox> OrderOutbox => Set<OrderOutbox>();
    public DbSet<OrderWebhookLog> OrderWebhookLogs => Set<OrderWebhookLog>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<TransferOrder> TransferOrders => Set<TransferOrder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("orders");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
