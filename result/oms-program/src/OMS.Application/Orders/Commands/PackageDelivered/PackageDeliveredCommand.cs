using MediatR;
using OMS.Application.Common.Models;

namespace OMS.Application.Orders.Commands.PackageDelivered;

public record PackageDeliveredCommand(
    Guid OrderId,
    string TrackingId,
    string UpdatedBy
) : IRequest<Result>;
