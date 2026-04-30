using MediatR;
using OMS.Application.Common.Models;
using OMS.Domain.Enums;

namespace OMS.Application.Orders.Commands.PlaceOrder;

public record PlaceOrderLineDto(
    string Sku,
    string ProductName,
    string Barcode,
    decimal RequestedAmount,
    UnitOfMeasure UnitOfMeasure,
    decimal UnitPrice,
    string Currency,
    bool IsSubstitute = false
);

public record PlaceOrderCommand(
    string OrderNumber,
    string? SourceOrderId,
    ChannelType ChannelType,
    string BusinessUnit,
    Guid StoreId,
    FulfillmentType FulfillmentType,
    PaymentMethod PaymentMethod,
    bool SubstitutionFlag,
    string CreatedBy,
    IReadOnlyList<PlaceOrderLineDto> OrderLines,
    DateTimeOffset? DeliverySlotStart,
    DateTimeOffset? DeliverySlotEnd
) : IRequest<Result<Guid>>;
