using MediatR;
using Microsoft.AspNetCore.Mvc;
using OMS.Application.Orders.Commands.AssignPackages;
using OMS.Application.Orders.Commands.CancelOrder;
using OMS.Application.Orders.Commands.ApproveSubstitution;
using OMS.Application.Orders.Commands.ConfirmBooking;
using OMS.Application.Orders.Commands.ConfirmPick;
using OMS.Application.Orders.Commands.RejectSubstitution;
using OMS.Application.Orders.Commands.GenerateInvoice;
using OMS.Application.Orders.Commands.HoldOrder;
using OMS.Application.Orders.Commands.MarkCollected;
using OMS.Application.Orders.Commands.MarkPacked;
using OMS.Application.Orders.Commands.MarkReadyForCollection;
using OMS.Application.Orders.Commands.ModifyOrderLines;
using OMS.Application.Orders.Commands.NotifyPayment;
using OMS.Application.Orders.Commands.PlaceOrder;
using OMS.Application.Orders.Commands.ReassignPackages;
using OMS.Application.Orders.Commands.ReleaseOrder;
using OMS.Application.Orders.Commands.RescheduleDelivery;
using OMS.Application.Orders.Queries.GetOrder;
using OMS.Application.Orders.Queries.GetOrderTimeline;
using OMS.Application.Orders.Queries.ListOrders;

namespace OMS.API.Controllers;

[ApiController]
[Route("api/v1/orders")]
public class OrdersController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> PlaceOrder([FromBody] PlaceOrderRequest request, CancellationToken cancellationToken)
    {
        var command = new PlaceOrderCommand(
            request.OrderNumber,
            request.SourceOrderId,
            request.ChannelType,
            request.BusinessUnit,
            request.StoreId,
            request.FulfillmentType,
            request.PaymentMethod,
            request.SubstitutionFlag,
            request.CreatedBy,
            request.OrderLines,
            request.DeliverySlotStart,
            request.DeliverySlotEnd);

        var result = await mediator.Send(command, cancellationToken);
        if (!result.Success)
            return Conflict(new { error = result.Error });

        return CreatedAtAction(nameof(GetOrder), new { orderId = result.Value }, new { orderId = result.Value });
    }

    [HttpGet("{orderId:guid}")]
    public async Task<IActionResult> GetOrder(Guid orderId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetOrderQuery(orderId), cancellationToken);
        if (!result.Success)
            return NotFound(new { error = result.Error });

        return Ok(result.Value);
    }

    [HttpGet("{orderId:guid}/timeline")]
    public async Task<IActionResult> GetOrderTimeline(Guid orderId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetOrderTimelineQuery(orderId), cancellationToken);
        if (!result.Success)
            return NotFound(new { error = result.Error });

        return Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> ListOrders(
        [FromQuery] string? status,
        [FromQuery] Guid? storeId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new ListOrdersQuery(status, storeId, page, pageSize), cancellationToken);
        return Ok(result.Value);
    }

    [HttpPatch("{orderId:guid}/confirm-booking")]
    public async Task<IActionResult> ConfirmBooking(Guid orderId, [FromBody] UpdatedByRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ConfirmBookingCommand(orderId, request.UpdatedBy), cancellationToken);
        if (!result.Success)
            return HandleFailure(result.Error!);

        return Ok();
    }

    [HttpPatch("{orderId:guid}/start-pick")]
    public async Task<IActionResult> StartPick(Guid orderId, [FromBody] UpdatedByRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new Application.Orders.Commands.StartPick.StartPickCommand(orderId, request.UpdatedBy), cancellationToken);
        if (!result.Success)
            return HandleFailure(result.Error!);

        return Ok();
    }

    [HttpPatch("{orderId:guid}/confirm-pick")]
    public async Task<IActionResult> ConfirmPick(Guid orderId, [FromBody] ConfirmPickRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ConfirmPickCommand(orderId, request.PickedLines, request.Substitutions ?? [], request.UpdatedBy), cancellationToken);
        if (!result.Success)
            return HandleFailure(result.Error!);

        return Ok();
    }

    [HttpPatch("{orderId:guid}/assign-packages")]
    public async Task<IActionResult> AssignPackages(Guid orderId, [FromBody] AssignPackagesRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new AssignPackagesCommand(orderId, request.Packages, request.UpdatedBy), cancellationToken);
        if (!result.Success)
            return HandleFailure(result.Error!);

        return Ok();
    }

    [HttpPatch("{orderId:guid}/pack")]
    public async Task<IActionResult> MarkPacked(Guid orderId, [FromBody] UpdatedByRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new MarkPackedCommand(orderId, request.UpdatedBy), cancellationToken);
        if (!result.Success)
            return HandleFailure(result.Error!);

        return Ok();
    }

    [HttpPatch("{orderId:guid}/reassign-packages")]
    public async Task<IActionResult> ReassignPackages(Guid orderId, [FromBody] ReassignPackagesRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ReassignPackagesCommand(orderId, request.Packages, request.UpdatedBy), cancellationToken);
        if (!result.Success)
            return HandleFailure(result.Error!);

        return Ok();
    }

    [HttpPatch("{orderId:guid}/modify-lines")]
    public async Task<IActionResult> ModifyOrderLines(Guid orderId, [FromBody] ModifyOrderLinesRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new ModifyOrderLinesCommand(orderId, request.AddLines, request.RemoveLineIds, request.ChangeQuantities, request.UpdatedBy),
            cancellationToken);
        if (!result.Success)
            return HandleFailure(result.Error!);

        return Ok();
    }

    [HttpPatch("{orderId:guid}/hold")]
    public async Task<IActionResult> HoldOrder(Guid orderId, [FromBody] HoldOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new HoldOrderCommand(orderId, request.HoldReason, request.HeldBy), cancellationToken);
        if (!result.Success)
            return HandleFailure(result.Error!);

        return Ok();
    }

    [HttpPatch("{orderId:guid}/release")]
    public async Task<IActionResult> ReleaseOrder(Guid orderId, [FromBody] UpdatedByRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ReleaseOrderCommand(orderId, request.UpdatedBy), cancellationToken);
        if (!result.Success)
            return HandleFailure(result.Error!);

        return Ok();
    }

    [HttpPatch("{orderId:guid}/cancel")]
    public async Task<IActionResult> CancelOrder(Guid orderId, [FromBody] CancelOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CancelOrderCommand(orderId, request.Reason, request.UpdatedBy), cancellationToken);
        if (!result.Success)
            return HandleFailure(result.Error!);

        return Ok();
    }

    [HttpPatch("{orderId:guid}/reschedule")]
    public async Task<IActionResult> RescheduleDelivery(Guid orderId, [FromBody] RescheduleDeliveryRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RescheduleDeliveryCommand(orderId, request.NewStart, request.NewEnd, request.UpdatedBy), cancellationToken);
        if (!result.Success)
            return HandleFailure(result.Error!);

        return Ok();
    }

    [HttpPatch("{orderId:guid}/ready-for-collection")]
    public async Task<IActionResult> MarkReadyForCollection(Guid orderId, [FromBody] UpdatedByRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new MarkReadyForCollectionCommand(orderId, request.UpdatedBy), cancellationToken);
        if (!result.Success)
            return HandleFailure(result.Error!);

        return Ok();
    }

    [HttpPatch("{orderId:guid}/collect")]
    public async Task<IActionResult> MarkCollected(Guid orderId, [FromBody] UpdatedByRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new MarkCollectedCommand(orderId, request.UpdatedBy), cancellationToken);
        if (!result.Success)
            return HandleFailure(result.Error!);

        return Ok();
    }

    [HttpPatch("{orderId:guid}/generate-invoice")]
    public async Task<IActionResult> GenerateInvoice(Guid orderId, [FromBody] GenerateInvoiceRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GenerateInvoiceCommand(orderId, request.InvoiceNumber, request.UpdatedBy), cancellationToken);
        if (!result.Success)
            return HandleFailure(result.Error!);

        return Ok();
    }

    [HttpPatch("{orderId:guid}/notify-payment")]
    public async Task<IActionResult> NotifyPayment(Guid orderId, [FromBody] UpdatedByRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new NotifyPaymentCommand(orderId, request.UpdatedBy), cancellationToken);
        if (!result.Success)
            return HandleFailure(result.Error!);

        return Ok();
    }

    [HttpPatch("{orderId:guid}/substitutions/{substitutionId:guid}/approve")]
    public async Task<IActionResult> ApproveSubstitution(Guid orderId, Guid substitutionId, [FromBody] UpdatedByRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ApproveSubstitutionCommand(orderId, substitutionId, request.UpdatedBy), cancellationToken);
        if (!result.Success)
            return HandleFailure(result.Error!);

        return Ok();
    }

    [HttpPatch("{orderId:guid}/substitutions/{substitutionId:guid}/reject")]
    public async Task<IActionResult> RejectSubstitution(Guid orderId, Guid substitutionId, [FromBody] UpdatedByRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RejectSubstitutionCommand(orderId, substitutionId, request.UpdatedBy), cancellationToken);
        if (!result.Success)
            return HandleFailure(result.Error!);

        return Ok();
    }

    private IActionResult HandleFailure(string error)
    {
        if (error.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return NotFound(new { error });

        return Conflict(new { error });
    }
}

public record PlaceOrderRequest(
    string OrderNumber,
    string? SourceOrderId,
    OMS.Domain.Enums.ChannelType ChannelType,
    string BusinessUnit,
    Guid StoreId,
    OMS.Domain.Enums.FulfillmentType FulfillmentType,
    OMS.Domain.Enums.PaymentMethod PaymentMethod,
    bool SubstitutionFlag,
    string CreatedBy,
    IReadOnlyList<PlaceOrderLineDto> OrderLines,
    DateTimeOffset? DeliverySlotStart,
    DateTimeOffset? DeliverySlotEnd
);

public record UpdatedByRequest(string UpdatedBy);

public record ConfirmPickRequest(
    IReadOnlyList<PickedLineDto> PickedLines,
    IReadOnlyList<SubstitutionDto>? Substitutions,
    string UpdatedBy
);

public record AssignPackagesRequest(
    IReadOnlyList<PackageGroupDto> Packages,
    string UpdatedBy
);

public record ReassignPackagesRequest(
    IReadOnlyList<ReassignPackageGroupDto> Packages,
    string UpdatedBy
);

public record ModifyOrderLinesRequest(
    IReadOnlyList<AddOrderLineDto> AddLines,
    IReadOnlyList<Guid> RemoveLineIds,
    IReadOnlyList<ChangeQuantityDto> ChangeQuantities,
    string UpdatedBy
);

public record HoldOrderRequest(string HoldReason, string HeldBy);

public record CancelOrderRequest(string Reason, string UpdatedBy);

public record RescheduleDeliveryRequest(DateTimeOffset NewStart, DateTimeOffset NewEnd, string UpdatedBy);

public record GenerateInvoiceRequest(string InvoiceNumber, string UpdatedBy);
