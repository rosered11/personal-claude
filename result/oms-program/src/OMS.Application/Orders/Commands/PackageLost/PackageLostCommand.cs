using MediatR;
using OMS.Application.Common.Models;

namespace OMS.Application.Orders.Commands.PackageLost;

public record PackageLostCommand(
    Guid OrderId,
    string TrackingId,
    string UpdatedBy
) : IRequest<Result>;
