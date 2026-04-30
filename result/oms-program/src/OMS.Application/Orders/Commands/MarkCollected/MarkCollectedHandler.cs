using MediatR;
using OMS.Application.Common.Interfaces;
using OMS.Application.Common.Models;
using OMS.Domain.Exceptions;

namespace OMS.Application.Orders.Commands.MarkCollected;

public class MarkCollectedHandler(
    IOrderRepository orderRepository,
    IOutboxPublisher outboxPublisher) : IRequestHandler<MarkCollectedCommand, Result>
{
    public async Task<Result> Handle(MarkCollectedCommand request, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result.Fail($"Order '{request.OrderId}' not found.");

        try
        {
            order.MarkCollected(request.UpdatedBy);

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
