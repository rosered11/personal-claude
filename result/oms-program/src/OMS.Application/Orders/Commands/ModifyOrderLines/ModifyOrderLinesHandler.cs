using MediatR;
using OMS.Application.Common.Interfaces;
using OMS.Application.Common.Models;
using OMS.Domain.Exceptions;

namespace OMS.Application.Orders.Commands.ModifyOrderLines;

public class ModifyOrderLinesHandler(
    IOrderRepository orderRepository,
    IOutboxPublisher outboxPublisher) : IRequestHandler<ModifyOrderLinesCommand, Result>
{
    public async Task<Result> Handle(ModifyOrderLinesCommand request, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result.Fail($"Order '{request.OrderId}' not found.");

        try
        {
            order.ModifyOrderLines(
                request.AddLines.Select(l => (l.Sku, l.ProductName, l.Barcode, l.RequestedAmount, l.UnitOfMeasure, l.UnitPrice, l.Currency)),
                request.RemoveLineIds,
                request.ChangeQuantities.Select(c => (c.OrderLineId, c.NewQuantity)),
                request.UpdatedBy);

            foreach (var evt in order.DomainEvents)
                await outboxPublisher.PublishAsync(order.OrderId, evt, cancellationToken);
            order.ClearDomainEvents();

            await orderRepository.SaveAsync(order, cancellationToken);
            return Result.Ok();
        }
        catch (OrderDomainException ex)
        {
            return Result.Fail(ex.Message);
        }
    }
}
