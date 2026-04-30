using OMS.Domain.Aggregates.ReturnAggregate;

namespace OMS.Application.Common.Interfaces;

public interface IReturnRepository
{
    Task<OrderReturn?> GetByIdAsync(Guid returnId, CancellationToken cancellationToken = default);
    Task<OrderReturn?> GetByReturnOrderNumberAsync(string returnOrderNumber, CancellationToken cancellationToken = default);
    Task SaveAsync(OrderReturn orderReturn, CancellationToken cancellationToken = default);
}
