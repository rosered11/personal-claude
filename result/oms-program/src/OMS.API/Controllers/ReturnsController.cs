using MediatR;
using Microsoft.AspNetCore.Mvc;
using OMS.Application.Returns.Commands.ConfirmPutAway;
using OMS.Application.Returns.Commands.ConfirmReceivedAtWarehouse;
using OMS.Application.Returns.Commands.ConfirmReturnPickedUp;
using OMS.Application.Returns.Commands.RequestReturn;
using OMS.Application.Returns.Commands.ScheduleReturnPickup;
using OMS.Application.Returns.Queries.GetReturn;

namespace OMS.API.Controllers;

[ApiController]
[Route("api/v1/returns")]
public class ReturnsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> RequestReturn([FromBody] RequestReturnRequest request, CancellationToken cancellationToken)
    {
        var command = new RequestReturnCommand(
            request.OrderId,
            request.ReturnOrderNumber,
            request.ReturnReason,
            request.InvoiceId,
            request.CreatedBy,
            request.Items);

        var result = await mediator.Send(command, cancellationToken);
        if (!result.Success)
            return Conflict(new { error = result.Error });

        return CreatedAtAction(nameof(GetReturn), new { returnId = result.Value }, new { returnId = result.Value });
    }

    [HttpGet("{returnId:guid}")]
    public async Task<IActionResult> GetReturn(Guid returnId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetReturnQuery(returnId), cancellationToken);
        if (!result.Success)
            return NotFound(new { error = result.Error });

        return Ok(result.Value);
    }

    [HttpPatch("{returnId:guid}/schedule-pickup")]
    public async Task<IActionResult> SchedulePickup(Guid returnId, [FromBody] SchedulePickupRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ScheduleReturnPickupCommand(returnId, request.PickupScheduledAt, request.UpdatedBy), cancellationToken);
        if (!result.Success)
            return HandleFailure(result.Error!);

        return Ok();
    }

    [HttpPatch("{returnId:guid}/confirm-pickup")]
    public async Task<IActionResult> ConfirmPickedUp(Guid returnId, [FromBody] UpdatedByReturnRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ConfirmReturnPickedUpCommand(returnId, request.UpdatedBy), cancellationToken);
        if (!result.Success)
            return HandleFailure(result.Error!);

        return Ok();
    }

    [HttpPatch("{returnId:guid}/confirm-received")]
    public async Task<IActionResult> ConfirmReceivedAtWarehouse(Guid returnId, [FromBody] ConfirmReceivedRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ConfirmReceivedAtWarehouseCommand(returnId, request.GoodsReceiveNo, request.UpdatedBy), cancellationToken);
        if (!result.Success)
            return HandleFailure(result.Error!);

        return Ok();
    }

    [HttpPatch("{returnId:guid}/put-away")]
    public async Task<IActionResult> ConfirmPutAway(Guid returnId, [FromBody] ConfirmPutAwayRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ConfirmPutAwayCommand(returnId, request.Items, request.UpdatedBy), cancellationToken);
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

public record RequestReturnRequest(
    Guid OrderId,
    string ReturnOrderNumber,
    string ReturnReason,
    string? InvoiceId,
    string CreatedBy,
    IReadOnlyList<ReturnItemRequestDto> Items
);

public record SchedulePickupRequest(DateTimeOffset PickupScheduledAt, string UpdatedBy);

public record UpdatedByReturnRequest(string UpdatedBy);

public record ConfirmReceivedRequest(string GoodsReceiveNo, string UpdatedBy);

public record ConfirmPutAwayRequest(IReadOnlyList<PutAwayItemDto> Items, string UpdatedBy);
