using Microsoft.EntityFrameworkCore;
using OMS.Application.Common.Interfaces;
using OMS.Domain.Aggregates.ReturnAggregate;

namespace OMS.Infrastructure.Persistence.Repositories;

public class ReturnRepository(OrderDbContext dbContext) : IReturnRepository
{
    public async Task<OrderReturn?> GetByIdAsync(Guid returnId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Returns
            .Include("_returnItems")
            .Include("_putAwayLogs")
            .FirstOrDefaultAsync(r => r.ReturnId == returnId, cancellationToken);
    }

    public async Task<OrderReturn?> GetByReturnOrderNumberAsync(string returnOrderNumber, CancellationToken cancellationToken = default)
    {
        return await dbContext.Returns
            .Include("_returnItems")
            .Include("_putAwayLogs")
            .FirstOrDefaultAsync(r => r.ReturnOrderNumber == returnOrderNumber, cancellationToken);
    }

    public async Task SaveAsync(OrderReturn orderReturn, CancellationToken cancellationToken = default)
    {
        var entry = dbContext.Entry(orderReturn);
        if (entry.State == EntityState.Detached)
            dbContext.Returns.Add(orderReturn);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
