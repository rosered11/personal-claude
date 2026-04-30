using MediatR;
using OMS.Application.Common.Interfaces;
using OMS.Application.Common.Models;
using OMS.Domain.Exceptions;

namespace OMS.Application.Inbound.TransferOrders.Commands.ConfirmTransferReceived;

public class ConfirmTransferReceivedHandler(
    ITransferOrderRepository transferOrderRepository,
    IOutboxPublisher outboxPublisher,
    IWebhookEventLogger webhookLogger) : IRequestHandler<ConfirmTransferReceivedCommand, Result>
{
    public async Task<Result> Handle(ConfirmTransferReceivedCommand request, CancellationToken cancellationToken)
    {
        var to = await transferOrderRepository.GetByIdAsync(request.TransferOrderId, cancellationToken);
        if (to is null)
            return Result.Fail($"Transfer order '{request.TransferOrderId}' not found.");

        try
        {
            webhookLogger.Stage(request.TransferOrderId, "WMS", "TransferReceived");

            to.ConfirmReceived(request.UpdatedBy);
            to.Complete(request.UpdatedBy);

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
