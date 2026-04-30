using MediatR;
using OMS.Application.Common.Interfaces;
using OMS.Application.Common.Models;
using OMS.Domain.Aggregates.OrderAggregate;
using OMS.Domain.Exceptions;

namespace OMS.Application.Orders.Commands.PlaceOrder;

public class PlaceOrderHandler(
    IOrderRepository orderRepository,
    IOutboxPublisher outboxPublisher) : IRequestHandler<PlaceOrderCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(PlaceOrderCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.SourceOrderId))
        {
            var existing = await orderRepository.GetBySourceOrderIdAsync(request.SourceOrderId, cancellationToken);
            if (existing is not null)
                return Result.Ok(existing.OrderId);
        }

        var existingByNumber = await orderRepository.GetByOrderNumberAsync(request.OrderNumber, cancellationToken);
        if (existingByNumber is not null)
            return Result.Fail<Guid>($"An order with number '{request.OrderNumber}' already exists.");

        try
        {
            var order = Order.Create(
                request.OrderNumber,
                request.SourceOrderId,
                request.ChannelType,
                request.BusinessUnit,
                request.StoreId,
                request.FulfillmentType,
                request.PaymentMethod,
                request.SubstitutionFlag,
                request.CreatedBy,
                request.OrderLines.Select(l => (
                    l.Sku,
                    l.ProductName,
                    l.Barcode,
                    l.RequestedAmount,
                    l.UnitOfMeasure,
                    l.UnitPrice,
                    l.Currency,
                    l.IsSubstitute
                )));

            if (request.DeliverySlotStart.HasValue && request.DeliverySlotEnd.HasValue)
                order.SetDeliverySlot(request.StoreId, request.DeliverySlotStart.Value, request.DeliverySlotEnd.Value);

            foreach (var evt in order.DomainEvents)
                await outboxPublisher.PublishAsync(order.OrderId, evt, cancellationToken);
            order.ClearDomainEvents();

            await orderRepository.SaveAsync(order, cancellationToken);

            return Result.Ok(order.OrderId);
        }
        catch (OrderDomainException ex)
        {
            return Result.Fail<Guid>(ex.Message);
        }
    }
}
