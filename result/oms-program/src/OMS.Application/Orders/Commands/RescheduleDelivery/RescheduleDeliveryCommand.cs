using MediatR;
using OMS.Application.Common.Models;

namespace OMS.Application.Orders.Commands.RescheduleDelivery;

public record RescheduleDeliveryCommand(
    Guid OrderId,
    DateTimeOffset NewStart,
    DateTimeOffset NewEnd,
    string UpdatedBy
) : IRequest<Result>;
