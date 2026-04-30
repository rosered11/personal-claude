using MediatR;
using OMS.Application.Common.Models;
using OMS.Domain.Enums;

namespace OMS.Application.Orders.Commands.ModifyOrderLines;

public record AddOrderLineDto(
    string Sku,
    string ProductName,
    string Barcode,
    decimal RequestedAmount,
    UnitOfMeasure UnitOfMeasure,
    decimal UnitPrice,
    string Currency
);

public record ChangeQuantityDto(Guid OrderLineId, decimal NewQuantity);

public record ModifyOrderLinesCommand(
    Guid OrderId,
    IReadOnlyList<AddOrderLineDto> AddLines,
    IReadOnlyList<Guid> RemoveLineIds,
    IReadOnlyList<ChangeQuantityDto> ChangeQuantities,
    string UpdatedBy
) : IRequest<Result>;
