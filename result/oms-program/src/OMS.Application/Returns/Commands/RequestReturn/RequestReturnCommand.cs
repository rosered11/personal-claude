using MediatR;
using OMS.Application.Common.Models;
using OMS.Domain.Enums;

namespace OMS.Application.Returns.Commands.RequestReturn;

public record ReturnItemRequestDto(
    Guid OrderLineId,
    string Sku,
    string ProductName,
    string Barcode,
    decimal Quantity,
    UnitOfMeasure UnitOfMeasure,
    decimal UnitPrice,
    string Currency,
    PaymentMethod PaymentMethod,
    string? ItemReason = null
);

public record RequestReturnCommand(
    Guid OrderId,
    string ReturnOrderNumber,
    string ReturnReason,
    string? InvoiceId,
    string CreatedBy,
    IReadOnlyList<ReturnItemRequestDto> Items
) : IRequest<Result<Guid>>;
