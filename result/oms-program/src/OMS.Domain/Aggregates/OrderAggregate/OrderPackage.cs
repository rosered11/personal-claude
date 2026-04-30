using OMS.Domain.Enums;
using OMS.Domain.Exceptions;

namespace OMS.Domain.Aggregates.OrderAggregate;

public class OrderPackage
{
    public Guid PackageId { get; private set; }
    public Guid OrderId { get; private set; }
    public string TrackingId { get; private set; } = null!;
    public string? CarrierPackageId { get; private set; }
    public string? ThirdPartyLogistic { get; private set; }
    public VehicleType VehicleType { get; private set; }
    public PackageStatus Status { get; private set; }
    public decimal PackageWeight { get; private set; }
    public string? DeliveryNoteNumber { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<OrderPackageLine> _packageLines = new();
    public IReadOnlyList<OrderPackageLine> PackageLines => _packageLines.AsReadOnly();

    public IReadOnlyList<Guid> OrderLineIds => _packageLines.Select(pl => pl.OrderLineId).ToList();

    private OrderPackage() { }

    public static OrderPackage Create(
        Guid orderId,
        string trackingId,
        VehicleType vehicleType,
        decimal packageWeight,
        IEnumerable<Guid> orderLineIds,
        string? carrierPackageId = null,
        string? thirdPartyLogistic = null,
        string? deliveryNoteNumber = null)
    {
        if (string.IsNullOrWhiteSpace(trackingId))
            throw new OrderDomainException("Tracking ID cannot be empty.");

        var pkg = new OrderPackage
        {
            PackageId = Guid.NewGuid(),
            OrderId = orderId,
            TrackingId = trackingId,
            CarrierPackageId = carrierPackageId,
            ThirdPartyLogistic = thirdPartyLogistic,
            VehicleType = vehicleType,
            Status = PackageStatus.Pending,
            PackageWeight = packageWeight,
            DeliveryNoteNumber = deliveryNoteNumber,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        foreach (var lineId in orderLineIds)
            pkg._packageLines.Add(OrderPackageLine.Create(pkg.PackageId, lineId));

        return pkg;
    }

    public void MarkOutForDelivery()
    {
        if (Status != PackageStatus.Pending)
            throw new OrderDomainException($"Package '{TrackingId}' cannot be marked out for delivery from status '{Status}'.");

        Status = PackageStatus.OutForDelivery;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkDelivered()
    {
        if (Status != PackageStatus.OutForDelivery)
            throw new OrderDomainException($"Package '{TrackingId}' cannot be marked delivered from status '{Status}'.");

        Status = PackageStatus.Delivered;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
