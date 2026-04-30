using MediatR;
using OMS.Application.Common.Interfaces;
using OMS.Application.Common.Models;
using OMS.Domain.Aggregates.TransferOrderAggregate;
using OMS.Domain.Exceptions;

namespace OMS.Application.Inbound.TransferOrders.Commands.CreateTransferOrder;

public class CreateTransferOrderHandler(
    ITransferOrderRepository transferOrderRepository,
    IOutboxPublisher outboxPublisher) : IRequestHandler<CreateTransferOrderCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateTransferOrderCommand request, CancellationToken cancellationToken)
    {
        var existing = await transferOrderRepository.GetByTransferNumberAsync(request.TransferNumber, cancellationToken);
        if (existing is not null)
            return Result<Guid>.Fail($"Transfer order '{request.TransferNumber}' already exists.");

        try
        {
            var to = TransferOrder.Create(
                request.TransferNumber,
                request.SourceStoreId,
                request.DestStoreId,
                request.CreatedBy,
                request.Lines.Select(l => (l.Sku, l.ProductName, l.RequestedQty, l.UnitOfMeasure)));

            foreach (var evt in to.DomainEvents)
                await outboxPublisher.PublishAsync(to.TransferOrderId, evt, cancellationToken);
            to.ClearDomainEvents();

            await transferOrderRepository.SaveAsync(to, cancellationToken);
            return Result<Guid>.Ok(to.TransferOrderId);
        }
        catch (OrderDomainException ex)
        {
            return Result<Guid>.Fail(ex.Message);
        }
    }
}
