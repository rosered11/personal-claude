using MediatR;
using OMS.Application.Common.Interfaces;
using OMS.Application.Common.Models;
using OMS.Domain.Exceptions;

namespace OMS.Application.Inbound.DamagedGoods.Commands.ConfirmDamagedGoodsReceived;

public class ConfirmDamagedGoodsReceivedHandler(
    IOrderRepository orderRepository,
    IOutboxPublisher outboxPublisher,
    IWebhookEventLogger webhookLogger) : IRequestHandler<ConfirmDamagedGoodsReceivedCommand, Result>
{
    public async Task<Result> Handle(ConfirmDamagedGoodsReceivedCommand request, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result.Fail($"Order '{request.OrderId}' not found.");

        try
        {
            webhookLogger.Stage(request.OrderId, "WMS", "DamagedGoodsReceived",
                $"tracking={request.TrackingId}");

            order.ConfirmDamagedGoodsReceived(request.TrackingId, request.UpdatedBy);

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
