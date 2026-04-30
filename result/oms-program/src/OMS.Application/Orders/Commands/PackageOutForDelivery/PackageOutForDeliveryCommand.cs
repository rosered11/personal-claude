using MediatR;
using OMS.Application.Common.Models;

namespace OMS.Application.Orders.Commands.PackageOutForDelivery;

public record PackageOutForDeliveryCommand(
    Guid OrderId,
    string TrackingId,
    string UpdatedBy
) : IRequest<Result>;
