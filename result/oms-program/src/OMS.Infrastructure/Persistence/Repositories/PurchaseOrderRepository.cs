using Microsoft.EntityFrameworkCore;
using OMS.Application.Common.Interfaces;
using OMS.Domain.Aggregates.PurchaseOrderAggregate;

namespace OMS.Infrastructure.Persistence.Repositories;

public class PurchaseOrderRepository(OrderDbContext dbContext) : IPurchaseOrderRepository
{
    public async Task<PurchaseOrder?> GetByIdAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default)
    {
        return await dbContext.PurchaseOrders
            .Include("_lines")
            .FirstOrDefaultAsync(p => p.PurchaseOrderId == purchaseOrderId, cancellationToken);
    }

    public async Task<PurchaseOrder?> GetByPoNumberAsync(string poNumber, CancellationToken cancellationToken = default)
    {
        return await dbContext.PurchaseOrders
            .Include("_lines")
            .FirstOrDefaultAsync(p => p.PoNumber == poNumber, cancellationToken);
    }

    public async Task SaveAsync(PurchaseOrder purchaseOrder, CancellationToken cancellationToken = default)
    {
        var entry = dbContext.Entry(purchaseOrder);
        if (entry.State == EntityState.Detached)
            dbContext.PurchaseOrders.Add(purchaseOrder);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
