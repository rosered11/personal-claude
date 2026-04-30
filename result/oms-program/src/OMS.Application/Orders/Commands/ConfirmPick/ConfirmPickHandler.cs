using MediatR;
using OMS.Application.Common.Interfaces;
using OMS.Application.Common.Models;
using OMS.Domain.Exceptions;

namespace OMS.Application.Orders.Commands.ConfirmPick;

public class ConfirmPickHandler(
    IOrderRepository orderRepository,
    IOutboxPublisher outboxPublisher,
    IWebhookEventLogger webhookLogger) : IRequestHandler<ConfirmPickCommand, Result>
{
    private static readonly HashSet<string> ExternalSystems =
        new(StringComparer.OrdinalIgnoreCase) { "WMS", "TMS", "POS" };

    public async Task<Result> Handle(ConfirmPickCommand request, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result.Fail($"Order '{request.OrderId}' not found.");

        try
        {
            if (ExternalSystems.Contains(request.UpdatedBy))
            {
                var detail = $"lines={request.PickedLines.Count}";
                if (request.Substitutions.Count > 0)
                    detail += $" substitutions={request.Substitutions.Count}";
                webhookLogger.Stage(request.OrderId, request.UpdatedBy, "PickConfirmed", detail);
            }

            order.ConfirmPick(
                request.PickedLines.Select(l => (l.OrderLineId, l.PickedAmount)),
                request.UpdatedBy);

            foreach (var sub in request.Substitutions)
            {
                order.RecordSubstitution(
                    sub.OriginalOrderLineId,
                    sub.SubstituteSku,
                    sub.SubstituteProductName,
                    sub.SubstituteBarcode,
                    sub.SubstituteUnitOfMeasure,
                    sub.SubstituteUnitPrice,
                    sub.SubstitutedAmount,
                    request.UpdatedBy);
            }

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
