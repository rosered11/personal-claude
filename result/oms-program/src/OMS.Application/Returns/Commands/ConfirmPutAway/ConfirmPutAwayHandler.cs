using MediatR;
using OMS.Application.Common.Interfaces;
using OMS.Application.Common.Models;
using OMS.Domain.Exceptions;

namespace OMS.Application.Returns.Commands.ConfirmPutAway;

public class ConfirmPutAwayHandler(
    IReturnRepository returnRepository,
    IOrderRepository orderRepository,
    IOutboxPublisher outboxPublisher,
    IWebhookEventLogger webhookLogger) : IRequestHandler<ConfirmPutAwayCommand, Result>
{
    public async Task<Result> Handle(ConfirmPutAwayCommand request, CancellationToken cancellationToken)
    {
        var ret = await returnRepository.GetByIdAsync(request.ReturnId, cancellationToken);
        if (ret is null)
            return Result.Fail($"Return '{request.ReturnId}' not found.");

        var order = await orderRepository.GetByIdAsync(ret.OrderId, cancellationToken);
        if (order is null)
            return Result.Fail($"Order '{ret.OrderId}' not found.");

        try
        {
            webhookLogger.Stage(ret.OrderId, "WMS", "PutAwayConfirmed",
                $"items={request.Items.Count}");

            ret.ConfirmPutAway(
                request.Items.Select(i => (i.ReturnItemId, i.Condition, i.AssignedSloc, i.Quantity, i.PerformedBy)),
                request.UpdatedBy);

            var creditNoteId = $"CN-{ret.ReturnId:N}";
            ret.ProcessRefund(creditNoteId, request.UpdatedBy);

            order.MarkReturned(ret.ReturnId, request.UpdatedBy);

            foreach (var evt in ret.DomainEvents)
                await outboxPublisher.PublishAsync(ret.OrderId, evt, cancellationToken);
            ret.ClearDomainEvents();

            foreach (var evt in order.DomainEvents)
                await outboxPublisher.PublishAsync(order.OrderId, evt, cancellationToken);
            order.ClearDomainEvents();

            await returnRepository.SaveAsync(ret, cancellationToken);
            await orderRepository.SaveAsync(order, cancellationToken);
            return Result.Ok();
        }
        catch (ReturnDomainException ex)
        {
            return Result.Fail(ex.Message);
        }
        catch (OrderDomainException ex)
        {
            return Result.Fail(ex.Message);
        }
    }
}
