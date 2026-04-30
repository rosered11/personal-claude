using Microsoft.EntityFrameworkCore;
using OMS.Application.Common.Interfaces;
using OMS.Domain.Aggregates.OrderAggregate;
using OMS.Domain.Enums;
using OMS.Infrastructure.Inbound;

namespace OMS.Infrastructure.Persistence.Repositories;

public class OrderRepository(OrderDbContext dbContext) : IOrderRepository
{
    private IQueryable<Order> GetOrdersWithIncludes() =>
        dbContext.Orders
            .Include("_orderLines")
            .Include("_packages")
            .Include("_packages._packageLines")
            .Include("_holds")
            .Include("_statusHistory")
            .Include("_substitutions");

    public async Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return await GetOrdersWithIncludes()
            .FirstOrDefaultAsync(o => o.OrderId == orderId, cancellationToken);
    }

    public async Task<Order?> GetByOrderNumberAsync(string orderNumber, CancellationToken cancellationToken = default)
    {
        return await GetOrdersWithIncludes()
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber, cancellationToken);
    }

    public async Task<Order?> GetBySourceOrderIdAsync(string sourceOrderId, CancellationToken cancellationToken = default)
    {
        return await GetOrdersWithIncludes()
            .FirstOrDefaultAsync(o => o.SourceOrderId == sourceOrderId, cancellationToken);
    }

    public async Task<(IReadOnlyList<Order> Items, int TotalCount)> ListAsync(
        string? status,
        Guid? storeId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = GetOrdersWithIncludes().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OrderStatus>(status, true, out var parsedStatus))
            query = query.Where(o => o.Status == parsedStatus);

        if (storeId.HasValue)
            query = query.Where(o => o.StoreId == storeId.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(o => o.OrderDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<OutboxEntrySnapshot>> GetOutboxEntriesAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.OrderOutbox
            .Where(o => o.OrderId == orderId)
            .OrderBy(o => o.CreatedAt)
            .Select(o => new OutboxEntrySnapshot(
                o.EventType,
                o.TargetSystem,
                o.Status.ToString(),
                o.RetryCount,
                o.CreatedAt,
                o.PublishedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WebhookLogSnapshot>> GetWebhookLogsAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.OrderWebhookLogs
            .Where(l => l.OrderId == orderId)
            .OrderBy(l => l.ReceivedAt)
            .Select(l => new WebhookLogSnapshot(
                l.SourceSystem,
                l.EventType,
                l.Detail,
                l.ReceivedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task SaveAsync(Order order, CancellationToken cancellationToken = default)
    {
        var entry = dbContext.Entry(order);
        if (entry.State == EntityState.Detached)
            dbContext.Orders.Add(order);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
