using MediatR;
using OMS.Application.Common.Interfaces;
using OMS.Application.Common.Models;
using OMS.Domain.Events;
using OMS.Domain.Exceptions;

namespace OMS.Application.Orders.Commands.RejectSubstitution;

public class RejectSubstitutionHandler(
    IOrderRepository orderRepository,
    IOutboxPublisher outboxPublisher) : IRequestHandler<RejectSubstitutionCommand, Result>
{
    public async Task<Result> Handle(RejectSubstitutionCommand request, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result.Fail($"Order '{request.OrderId}' not found.");

        try
        {
            order.RejectSubstitution(request.SubstitutionId, request.UpdatedBy);

            foreach (var evt in order.DomainEvents)
                await outboxPublisher.PublishAsync(order.OrderId, evt, cancellationToken);
            order.ClearDomainEvents();

            // Rejection removes a substitute line — re-trigger POS recalc if all substitutions resolved
            if (!order.HasPendingSubstitutions())
                await outboxPublisher.PublishAsync(order.OrderId,
                    new PickConfirmedEvent(order.OrderId, hasPartialPick: true), cancellationToken);

            await orderRepository.SaveAsync(order, cancellationToken);
            return Result.Ok();
        }
        catch (OrderDomainException ex)
        {
            return Result.Fail(ex.Message);
        }
    }
}
