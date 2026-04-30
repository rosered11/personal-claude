using MediatR;
using OMS.Application.Common.Models;
using OMS.Domain.Enums;

namespace OMS.Application.Orders.Commands.ReassignPackages;

public record ReassignPackageGroupDto(
    string TrackingId,
    VehicleType VehicleType,
    decimal PackageWeight,
    IReadOnlyList<Guid> OrderLineIds,
    string? CarrierPackageId,
    string? ThirdPartyLogistic,
    string? DeliveryNoteNumber
);

public record ReassignPackagesCommand(
    Guid OrderId,
    IReadOnlyList<ReassignPackageGroupDto> Packages,
    string UpdatedBy
) : IRequest<Result>;
