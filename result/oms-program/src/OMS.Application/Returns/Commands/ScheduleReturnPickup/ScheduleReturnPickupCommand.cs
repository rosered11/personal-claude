using MediatR;
using OMS.Application.Common.Models;

namespace OMS.Application.Returns.Commands.ScheduleReturnPickup;

public record ScheduleReturnPickupCommand(
    Guid ReturnId,
    DateTimeOffset PickupScheduledAt,
    string UpdatedBy
) : IRequest<Result>;
