using MediatR;
using Microsoft.AspNetCore.Mvc;
using OMS.Application.Returns.Commands.ConfirmReturnPickedUp;
using OMS.Application.Returns.Commands.ConfirmReceivedAtWarehouse;
using OMS.Application.Returns.Commands.ScheduleReturnPickup;
using OMS.Application.Inbound.DamagedGoods.Commands.ConfirmDamagedGoodsReceived;
using OMS.Application.Inbound.DamagedGoods.Commands.ConfirmDamagedGoodsPutAway;
using OMS.Application.Inbound.PurchaseOrders.Commands.ConfirmGoodsReceipt;
using OMS.Application.Inbound.PurchaseOrders.Commands.ConfirmPurchaseOrderPutAway;
using OMS.Application.Inbound.TransferOrders.Commands.ConfirmTransferPick;
using OMS.Application.Inbound.TransferOrders.Commands.ConfirmTransferReceived;
using OMS.Application.Orders.Commands.ApplyPosRecalculation;
using OMS.Application.Orders.Commands.PackageDamaged;
using OMS.Application.Orders.Commands.PackageDelivered;
using OMS.Application.Orders.Commands.PackageLost;
using OMS.Application.Orders.Commands.PackageOutForDelivery;
using OMS.Application.Returns.Commands.ConfirmPutAway;

namespace OMS.API.Controllers;

[ApiController]
[Route("webhooks")]
public class WebhookController(IMediator mediator, ILogger<WebhookController> logger) : ControllerBase
{
    [HttpPost("wms/pick-confirmed")]
    public async Task<IActionResult> WmsPickConfirmed([FromBody] WmsPickConfirmedWebhook payload, CancellationToken cancellationToken)
    {
        logger.LogInformation("Inbound WMS webhook: PickConfirmed orderId={OrderId} lines={LineCount}",
            payload.OrderId, payload.PickedLines.Count);

        var command = new Application.Orders.Commands.ConfirmPick.ConfirmPickCommand(
            payload.OrderId, payload.PickedLines, payload.Substitutions ?? [], "WMS");

        var result = await mediator.Send(command, cancellationToken);
        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return Accepted();
    }

    [HttpPost("wms/put-away-confirmed")]
    public async Task<IActionResult> WmsPutAwayConfirmed([FromBody] WmsPutAwayConfirmedWebhook payload, CancellationToken cancellationToken)
    {
        logger.LogInformation("Inbound WMS webhook: PutAwayConfirmed returnId={ReturnId} items={ItemCount}",
            payload.ReturnId, payload.Items.Count);

        var command = new ConfirmPutAwayCommand(payload.ReturnId, payload.Items, "WMS");
        var result = await mediator.Send(command, cancellationToken);
        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return Accepted();
    }

    [HttpPost("tms/package-dispatched")]
    public async Task<IActionResult> TmsPackageDispatched([FromBody] TmsPackageWebhook payload, CancellationToken cancellationToken)
    {
        logger.LogInformation("Inbound TMS webhook: PackageDispatched orderId={OrderId} trackingId={TrackingId}",
            payload.OrderId, payload.TrackingId);

        var command = new PackageOutForDeliveryCommand(payload.OrderId, payload.TrackingId, "TMS");
        var result = await mediator.Send(command, cancellationToken);
        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return Accepted();
    }

    [HttpPost("tms/package-delivered")]
    public async Task<IActionResult> TmsPackageDelivered([FromBody] TmsPackageWebhook payload, CancellationToken cancellationToken)
    {
        logger.LogInformation("Inbound TMS webhook: PackageDelivered orderId={OrderId} trackingId={TrackingId}",
            payload.OrderId, payload.TrackingId);

        var command = new PackageDeliveredCommand(payload.OrderId, payload.TrackingId, "TMS");
        var result = await mediator.Send(command, cancellationToken);
        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return Accepted();
    }

    [HttpPost("tms/package-lost")]
    public async Task<IActionResult> TmsPackageLost([FromBody] TmsPackageWebhook payload, CancellationToken cancellationToken)
    {
        logger.LogInformation("Inbound TMS webhook: PackageLost orderId={OrderId} trackingId={TrackingId}",
            payload.OrderId, payload.TrackingId);

        var command = new PackageLostCommand(payload.OrderId, payload.TrackingId, "TMS");
        var result = await mediator.Send(command, cancellationToken);
        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return Accepted();
    }

    [HttpPost("tms/package-damaged")]
    public async Task<IActionResult> TmsPackageDamaged([FromBody] TmsPackageWebhook payload, CancellationToken cancellationToken)
    {
        logger.LogInformation("Inbound TMS webhook: PackageDamaged orderId={OrderId} trackingId={TrackingId}",
            payload.OrderId, payload.TrackingId);

        var command = new PackageDamagedCommand(payload.OrderId, payload.TrackingId, "TMS");
        var result = await mediator.Send(command, cancellationToken);
        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return Accepted();
    }

    [HttpPost("pos/recalculation-result")]
    public async Task<IActionResult> PosRecalculationResult([FromBody] PosRecalculationWebhook payload, CancellationToken cancellationToken)
    {
        logger.LogInformation("Inbound POS webhook: RecalculationResult orderId={OrderId}", payload.OrderId);

        var command = new ApplyPosRecalculationCommand(payload.OrderId, "POS");
        var result = await mediator.Send(command, cancellationToken);
        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return Accepted();
    }

    // --- Return lifecycle webhooks ---

    [HttpPost("tms/return-pickup-scheduled")]
    public async Task<IActionResult> TmsReturnPickupScheduled([FromBody] TmsReturnPickupScheduledWebhook payload, CancellationToken cancellationToken)
    {
        logger.LogInformation("Inbound TMS webhook: ReturnPickupScheduled returnId={ReturnId}", payload.ReturnId);

        var command = new ScheduleReturnPickupCommand(payload.ReturnId, payload.PickupScheduledAt, "TMS");
        var result = await mediator.Send(command, cancellationToken);
        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return Accepted();
    }

    [HttpPost("tms/return-pickup-confirmed")]
    public async Task<IActionResult> TmsReturnPickupConfirmed([FromBody] TmsReturnWebhook payload, CancellationToken cancellationToken)
    {
        logger.LogInformation("Inbound TMS webhook: ReturnPickupConfirmed returnId={ReturnId}", payload.ReturnId);

        var command = new ConfirmReturnPickedUpCommand(payload.ReturnId, "TMS");
        var result = await mediator.Send(command, cancellationToken);
        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return Accepted();
    }

    [HttpPost("wms/return-received-at-warehouse")]
    public async Task<IActionResult> WmsReturnReceivedAtWarehouse([FromBody] WmsReturnReceivedWebhook payload, CancellationToken cancellationToken)
    {
        logger.LogInformation("Inbound WMS webhook: ReturnReceivedAtWarehouse returnId={ReturnId}", payload.ReturnId);

        var command = new ConfirmReceivedAtWarehouseCommand(payload.ReturnId, payload.GoodsReceiveNo, "WMS");
        var result = await mediator.Send(command, cancellationToken);
        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return Accepted();
    }

    // --- Inbound (warehouse receiving) webhooks ---

    [HttpPost("wms/goods-receipt-confirmed")]
    public async Task<IActionResult> WmsGoodsReceiptConfirmed([FromBody] WmsGoodsReceiptWebhook payload, CancellationToken cancellationToken)
    {
        logger.LogInformation("Inbound WMS webhook: GoodsReceiptConfirmed purchaseOrderId={PoId} lines={LineCount}",
            payload.PurchaseOrderId, payload.Lines.Count);

        var command = new ConfirmGoodsReceiptCommand(payload.PurchaseOrderId, payload.Lines, "WMS");
        var result = await mediator.Send(command, cancellationToken);
        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return Accepted();
    }

    [HttpPost("wms/purchase-order-put-away-confirmed")]
    public async Task<IActionResult> WmsPurchaseOrderPutAwayConfirmed([FromBody] WmsPurchaseOrderPutAwayWebhook payload, CancellationToken cancellationToken)
    {
        logger.LogInformation("Inbound WMS webhook: PurchaseOrderPutAwayConfirmed purchaseOrderId={PoId}",
            payload.PurchaseOrderId);

        var command = new ConfirmPurchaseOrderPutAwayCommand(payload.PurchaseOrderId, "WMS");
        var result = await mediator.Send(command, cancellationToken);
        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return Accepted();
    }

    [HttpPost("wms/transfer-pick-confirmed")]
    public async Task<IActionResult> WmsTransferPickConfirmed([FromBody] WmsTransferPickWebhook payload, CancellationToken cancellationToken)
    {
        logger.LogInformation("Inbound WMS webhook: TransferPickConfirmed transferOrderId={ToId} lines={LineCount}",
            payload.TransferOrderId, payload.Lines.Count);

        var command = new ConfirmTransferPickCommand(payload.TransferOrderId, payload.Lines, "WMS");
        var result = await mediator.Send(command, cancellationToken);
        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return Accepted();
    }

    [HttpPost("wms/transfer-received")]
    public async Task<IActionResult> WmsTransferReceived([FromBody] WmsTransferReceivedWebhook payload, CancellationToken cancellationToken)
    {
        logger.LogInformation("Inbound WMS webhook: TransferReceived transferOrderId={ToId}", payload.TransferOrderId);

        var command = new ConfirmTransferReceivedCommand(payload.TransferOrderId, "WMS");
        var result = await mediator.Send(command, cancellationToken);
        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return Accepted();
    }

    [HttpPost("wms/damaged-goods-received")]
    public async Task<IActionResult> WmsDamagedGoodsReceived([FromBody] WmsDamagedGoodsReceivedWebhook payload, CancellationToken cancellationToken)
    {
        logger.LogInformation("Inbound WMS webhook: DamagedGoodsReceived orderId={OrderId} trackingId={TrackingId}",
            payload.OrderId, payload.TrackingId);

        var command = new ConfirmDamagedGoodsReceivedCommand(payload.OrderId, payload.TrackingId, "WMS");
        var result = await mediator.Send(command, cancellationToken);
        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return Accepted();
    }

    [HttpPost("wms/damaged-goods-put-away-confirmed")]
    public async Task<IActionResult> WmsDamagedGoodsPutAwayConfirmed([FromBody] WmsDamagedGoodsPutAwayWebhook payload, CancellationToken cancellationToken)
    {
        logger.LogInformation("Inbound WMS webhook: DamagedGoodsPutAwayConfirmed orderId={OrderId} trackingId={TrackingId}",
            payload.OrderId, payload.TrackingId);

        var command = new ConfirmDamagedGoodsPutAwayCommand(payload.OrderId, payload.TrackingId, payload.Items, "WMS");
        var result = await mediator.Send(command, cancellationToken);
        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return Accepted();
    }
}

public record WmsPickConfirmedWebhook(
    Guid OrderId,
    IReadOnlyList<Application.Orders.Commands.ConfirmPick.PickedLineDto> PickedLines,
    IReadOnlyList<Application.Orders.Commands.ConfirmPick.SubstitutionDto>? Substitutions
);

public record WmsPutAwayConfirmedWebhook(
    Guid ReturnId,
    IReadOnlyList<PutAwayItemDto> Items
);

public record TmsPackageWebhook(Guid OrderId, string TrackingId);

public record PosRecalculationWebhook(Guid OrderId);

public record WmsGoodsReceiptWebhook(
    Guid PurchaseOrderId,
    IReadOnlyList<GoodsReceiptLineDto> Lines);

public record WmsPurchaseOrderPutAwayWebhook(Guid PurchaseOrderId);

public record WmsTransferPickWebhook(
    Guid TransferOrderId,
    IReadOnlyList<TransferPickLineDto> Lines);

public record WmsTransferReceivedWebhook(Guid TransferOrderId);

public record WmsDamagedGoodsReceivedWebhook(Guid OrderId, string TrackingId);

public record WmsDamagedGoodsPutAwayWebhook(
    Guid OrderId,
    string TrackingId,
    IReadOnlyList<DamagedItemPutAwayDto> Items);

public record TmsReturnPickupScheduledWebhook(Guid ReturnId, DateTimeOffset PickupScheduledAt);

public record TmsReturnWebhook(Guid ReturnId);

public record WmsReturnReceivedWebhook(Guid ReturnId, string GoodsReceiveNo);
