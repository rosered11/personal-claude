using MediatR;
using OMS.Application.Common.Models;

namespace OMS.Application.Orders.Commands.PackageDamaged;

public record PackageDamagedCommand(
    Guid OrderId,
    string TrackingId,
    string UpdatedBy
) : IRequest<Result>;
