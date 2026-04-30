using OMS.Domain.Aggregates.TransferOrderAggregate;

namespace OMS.Application.Common.Interfaces;

public interface ITransferOrderRepository
{
    Task<TransferOrder?> GetByIdAsync(Guid transferOrderId, CancellationToken cancellationToken = default);
    Task<TransferOrder?> GetByTransferNumberAsync(string transferNumber, CancellationToken cancellationToken = default);
    Task SaveAsync(TransferOrder transferOrder, CancellationToken cancellationToken = default);
}
