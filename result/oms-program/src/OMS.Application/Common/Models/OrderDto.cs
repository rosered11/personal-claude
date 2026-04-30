using OMS.Domain.Enums;

namespace OMS.Application.Common.Models;

public record OrderDto(
    Guid OrderId,
    string OrderNumber,
    string? SourceOrderId,
    string ChannelType,
    string BusinessUnit,
    Guid StoreId,
    DateTimeOffset OrderDate,
    string Status,
    string? PreHoldStatus,
    string? HoldReason,
    string FulfillmentType,
    string PaymentMethod,
    bool SubstitutionFlag,
    bool PosRecalcPending,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string CreatedBy,
    string UpdatedBy,
    IReadOnlyList<OrderLineDto> OrderLines,
    IReadOnlyList<OrderPackageDto> Packages,
    DeliverySlotDto? DeliverySlot
);

public record OrderLineDto(
    Guid OrderLineId,
    string Sku,
    string ProductName,
    string Barcode,
    decimal RequestedAmount,
    decimal PickedAmount,
    string UnitOfMeasure,
    decimal OriginalUnitPrice,
    string Currency,
    string Status,
    bool IsSubstitute
);

public record OrderPackageDto(
    Guid PackageId,
    string TrackingId,
    string? CarrierPackageId,
    string? ThirdPartyLogistic,
    string VehicleType,
    string Status,
    decimal PackageWeight,
    string? DeliveryNoteNumber,
    IReadOnlyList<Guid> OrderLineIds
);

public record DeliverySlotDto(
    Guid SlotId,
    Guid StoreId,
    DateTimeOffset ScheduledStart,
    DateTimeOffset ScheduledEnd
);
