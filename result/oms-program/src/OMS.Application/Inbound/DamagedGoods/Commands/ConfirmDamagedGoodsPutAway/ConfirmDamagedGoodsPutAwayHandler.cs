using MediatR;
using OMS.Application.Common.Interfaces;
using OMS.Application.Common.Models;
using OMS.Domain.Exceptions;

namespace OMS.Application.Inbound.DamagedGoods.Commands.ConfirmDamagedGoodsPutAway;

public class ConfirmDamagedGoodsPutAwayHandler(
    IOrderRepository orderRepository,
    IOutboxPublisher outboxPublisher,
    IWebhookEventLogger webhookLogger) : IRequestHandler<ConfirmDamagedGoodsPutAwayCommand, Result>
{
    public async Task<Result> Handle(ConfirmDamagedGoodsPutAwayCommand request, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result.Fail($"Order '{request.OrderId}' not found.");

        try
        {
            var itemSummary = string.Join(", ", request.Items.Select(i => $"{i.Sku}={i.Condition}@{i.AssignedSloc}"));
            webhookLogger.Stage(request.OrderId, "WMS", "DamagedGoodsPutAwayConfirmed",
                $"tracking={request.TrackingId} items=[{itemSummary}]");

            order.ConfirmDamagedGoodsPutAway(request.TrackingId, request.UpdatedBy);

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
