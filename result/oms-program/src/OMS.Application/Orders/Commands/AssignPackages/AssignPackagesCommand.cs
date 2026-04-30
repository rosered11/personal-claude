using MediatR;
using OMS.Application.Common.Models;
using OMS.Domain.Enums;

namespace OMS.Application.Orders.Commands.AssignPackages;

public record PackageGroupDto(
    string TrackingId,
    VehicleType VehicleType,
    decimal PackageWeight,
    IReadOnlyList<Guid> OrderLineIds,
    string? CarrierPackageId,
    string? ThirdPartyLogistic,
    string? DeliveryNoteNumber
);

public record AssignPackagesCommand(
    Guid OrderId,
    IReadOnlyList<PackageGroupDto> Packages,
    string UpdatedBy
) : IRequest<Result>;
