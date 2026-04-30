using MediatR;
using OMS.Application.Common.Interfaces;
using OMS.Application.Common.Models;
using OMS.Domain.Exceptions;

namespace OMS.Application.Inbound.PurchaseOrders.Commands.ConfirmGoodsReceipt;

public class ConfirmGoodsReceiptHandler(
    IPurchaseOrderRepository purchaseOrderRepository,
    IOutboxPublisher outboxPublisher,
    IWebhookEventLogger webhookLogger) : IRequestHandler<ConfirmGoodsReceiptCommand, Result>
{
    public async Task<Result> Handle(ConfirmGoodsReceiptCommand request, CancellationToken cancellationToken)
    {
        var po = await purchaseOrderRepository.GetByIdAsync(request.PurchaseOrderId, cancellationToken);
        if (po is null)
            return Result.Fail($"Purchase order '{request.PurchaseOrderId}' not found.");

        try
        {
            webhookLogger.Stage(request.PurchaseOrderId, "WMS", "GoodsReceiptConfirmed",
                $"lines={request.Lines.Count}");

            po.ConfirmGoodsReceipt(
                request.Lines.Select(l => (l.LineId, l.ReceivedQty, l.Condition)),
                request.UpdatedBy);

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
