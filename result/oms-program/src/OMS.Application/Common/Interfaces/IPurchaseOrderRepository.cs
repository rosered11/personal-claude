using OMS.Domain.Aggregates.PurchaseOrderAggregate;

namespace OMS.Application.Common.Interfaces;

public interface IPurchaseOrderRepository
{
    Task<PurchaseOrder?> GetByIdAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default);
    Task<PurchaseOrder?> GetByPoNumberAsync(string poNumber, CancellationToken cancellationToken = default);
    Task SaveAsync(PurchaseOrder purchaseOrder, CancellationToken cancellationToken = default);
}
