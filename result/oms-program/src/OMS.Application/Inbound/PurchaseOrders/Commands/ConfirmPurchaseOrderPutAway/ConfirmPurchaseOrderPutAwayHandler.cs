using MediatR;
using OMS.Application.Common.Interfaces;
using OMS.Application.Common.Models;
using OMS.Domain.Exceptions;

namespace OMS.Application.Inbound.PurchaseOrders.Commands.ConfirmPurchaseOrderPutAway;

public class ConfirmPurchaseOrderPutAwayHandler(
    IPurchaseOrderRepository purchaseOrderRepository,
    IOutboxPublisher outboxPublisher,
    IWebhookEventLogger webhookLogger) : IRequestHandler<ConfirmPurchaseOrderPutAwayCommand, Result>
{
    public async Task<Result> Handle(ConfirmPurchaseOrderPutAwayCommand request, CancellationToken cancellationToken)
    {
        var po = await purchaseOrderRepository.GetByIdAsync(request.PurchaseOrderId, cancellationToken);
        if (po is null)
            return Result.Fail($"Purchase order '{request.PurchaseOrderId}' not found.");

        try
        {
            webhookLogger.Stage(request.PurchaseOrderId, "WMS", "PutAwayConfirmed");

            po.ConfirmPutAway(request.UpdatedBy);

            foreach (var evt in po.DomainEvents)
                await outboxPublisher.PublishAsync(po.PurchaseOrderId, evt, cancellationToken);
            po.ClearDomainEvents();

            await purchaseOrderRepository.SaveAsync(po, cancellationToken);
            return Result.Ok();
        }
        catch (OrderDomainException ex)
        {
            return Result.Fail(ex.Message);
        }
    }
}
