using Microsoft.EntityFrameworkCore;
using OMS.Application.Common.Interfaces;
using OMS.Domain.Aggregates.TransferOrderAggregate;

namespace OMS.Infrastructure.Persistence.Repositories;

public class TransferOrderRepository(OrderDbContext dbContext) : ITransferOrderRepository
{
    public async Task<TransferOrder?> GetByIdAsync(Guid transferOrderId, CancellationToken cancellationToken = default)
    {
        return await dbContext.TransferOrders
            .Include("_lines")
            .FirstOrDefaultAsync(t => t.TransferOrderId == transferOrderId, cancellationToken);
    }

    public async Task<TransferOrder?> GetByTransferNumberAsync(string transferNumber, CancellationToken cancellationToken = default)
    {
        return await dbContext.TransferOrders
            .Include("_lines")
            .FirstOrDefaultAsync(t => t.TransferNumber == transferNumber, cancellationToken);
    }

    public async Task SaveAsync(TransferOrder transferOrder, CancellationToken cancellationToken = default)
    {
        var entry = dbContext.Entry(transferOrder);
        if (entry.State == EntityState.Detached)
            dbContext.TransferOrders.Add(transferOrder);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
