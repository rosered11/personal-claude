using MediatR;
using OMS.Application.Common.Interfaces;
using OMS.Application.Common.Models;
using OMS.Domain.Aggregates.PurchaseOrderAggregate;
using OMS.Domain.Exceptions;

namespace OMS.Application.Inbound.PurchaseOrders.Commands.CreatePurchaseOrder;

public class CreatePurchaseOrderHandler(
    IPurchaseOrderRepository purchaseOrderRepository,
    IOutboxPublisher outboxPublisher) : IRequestHandler<CreatePurchaseOrderCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreatePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var existing = await purchaseOrderRepository.GetByPoNumberAsync(request.PoNumber, cancellationToken);
        if (existing is not null)
            return Result<Guid>.Fail($"Purchase order '{request.PoNumber}' already exists.");

        try
        {
            var po = PurchaseOrder.Create(
                request.PoNumber,
                request.SupplierId,
                request.StoreId,
                request.CreatedBy,
                request.Lines.Select(l => (l.Sku, l.ProductName, l.OrderedQty, l.UnitOfMeasure)));

            foreach (var evt in po.DomainEvents)
                await outboxPublisher.PublishAsync(po.PurchaseOrderId, evt, cancellationToken);
            po.ClearDomainEvents();

            await purchaseOrderRepository.SaveAsync(po, cancellationToken);
            return Result<Guid>.Ok(po.PurchaseOrderId);
        }
        catch (OrderDomainException ex)
        {
            return Result<Guid>.Fail(ex.Message);
        }
    }
}
