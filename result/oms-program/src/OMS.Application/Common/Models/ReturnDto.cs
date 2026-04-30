namespace OMS.Application.Common.Models;

public record ReturnDto(
    Guid ReturnId,
    Guid OrderId,
    string ReturnOrderNumber,
    string? InvoiceId,
    string? CreditNoteId,
    string Status,
    string? GoodsReceiveNo,
    string ReturnReason,
    DateTimeOffset RequestedAt,
    DateTimeOffset? PickupScheduledAt,
    DateTimeOffset? PickedUpAt,
    DateTimeOffset? ReceivedAt,
    DateTimeOffset? InspectedAt,
    DateTimeOffset? PutAwayAt,
    DateTimeOffset? RefundedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string CreatedBy,
    string UpdatedBy,
    IReadOnlyList<ReturnItemDto> ReturnItems
);

public record ReturnItemDto(
    Guid ReturnItemId,
    Guid OrderLineId,
    string Sku,
    string ProductName,
    string Barcode,
    decimal Quantity,
    string UnitOfMeasure,
    decimal UnitPrice,
    string Currency,
    string? ItemReason,
    string? Condition,
    string? PutAwayStatus,
    string? AssignedSloc,
    string PaymentMethod
);
