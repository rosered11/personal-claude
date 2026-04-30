using MediatR;
using OMS.Application.Common.Interfaces;
using OMS.Application.Common.Models;
using OMS.Domain.Exceptions;

namespace OMS.Application.Inbound.TransferOrders.Commands.ConfirmTransferPick;

public class ConfirmTransferPickHandler(
    ITransferOrderRepository transferOrderRepository,
    IOutboxPublisher outboxPublisher,
    IWebhookEventLogger webhookLogger) : IRequestHandler<ConfirmTransferPickCommand, Result>
{
    public async Task<Result> Handle(ConfirmTransferPickCommand request, CancellationToken cancellationToken)
    {
        var to = await transferOrderRepository.GetByIdAsync(request.TransferOrderId, cancellationToken);
        if (to is null)
            return Result.Fail($"Transfer order '{request.TransferOrderId}' not found.");

        try
        {
            webhookLogger.Stage(request.TransferOrderId, "WMS", "TransferPickConfirmed",
                $"lines={request.Lines.Count}");

            to.ConfirmPick(
                request.Lines.Select(l => (l.LineId, l.TransferredQty)),
                request.UpdatedBy);

            foreach (var evt in to.DomainEvents)
                await outboxPublisher.PublishAsync(to.TransferOrderId, evt, cancellationToken);
            to.ClearDomainEvents();

            await transferOrderRepository.SaveAsync(to, cancellationToken);
            return Result.Ok();
        }
        catch (OrderDomainException ex)
        {
            return Result.Fail(ex.Message);
        }
    }
}
