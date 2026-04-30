using MediatR;
using Microsoft.AspNetCore.Mvc;
using OMS.Application.Inbound.PurchaseOrders.Commands.CreatePurchaseOrder;
using OMS.Application.Inbound.TransferOrders.Commands.CreateTransferOrder;
using OMS.Domain.Enums;

namespace OMS.API.Controllers;

[ApiController]
[Route("api/v1/inbound")]
public class InboundController(IMediator mediator) : ControllerBase
{
    // --- Purchase Orders ---

    [HttpPost("purchase-orders")]
    public async Task<IActionResult> CreatePurchaseOrder([FromBody] CreatePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        var command = new CreatePurchaseOrderCommand(
            request.PoNumber,
            request.SupplierId,
            request.StoreId,
            request.Lines.Select(l => new CreatePurchaseOrderLineDto(l.Sku, l.ProductName, l.OrderedQty, l.UnitOfMeasure)).ToList(),
            request.CreatedBy);

        var result = await mediator.Send(command, cancellationToken);
        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return CreatedAtAction(null, new { id = result.Value });
    }

    // --- Transfer Orders ---

    [HttpPost("transfer-orders")]
    public async Task<IActionResult> CreateTransferOrder([FromBody] CreateTransferOrderRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateTransferOrderCommand(
            request.TransferNumber,
            request.SourceStoreId,
            request.DestStoreId,
            request.Lines.Select(l => new CreateTransferOrderLineDto(l.Sku, l.ProductName, l.RequestedQty, l.UnitOfMeasure)).ToList(),
            request.CreatedBy);

        var result = await mediator.Send(command, cancellationToken);
        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return CreatedAtAction(null, new { id = result.Value });
    }
}

public record CreatePurchaseOrderLineRequest(string Sku, string ProductName, decimal OrderedQty, UnitOfMeasure UnitOfMeasure);

public record CreatePurchaseOrderRequest(
    string PoNumber,
    string SupplierId,
    Guid StoreId,
    IReadOnlyList<CreatePurchaseOrderLineRequest> Lines,
    string CreatedBy);

public record CreateTransferOrderLineRequest(string Sku, string ProductName, decimal RequestedQty, UnitOfMeasure UnitOfMeasure);

public record CreateTransferOrderRequest(
    string TransferNumber,
    Guid SourceStoreId,
    Guid DestStoreId,
    IReadOnlyList<CreateTransferOrderLineRequest> Lines,
    string CreatedBy);
