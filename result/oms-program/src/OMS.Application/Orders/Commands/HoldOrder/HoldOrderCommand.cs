using MediatR;
using OMS.Application.Common.Models;

namespace OMS.Application.Orders.Commands.HoldOrder;

public record HoldOrderCommand(Guid OrderId, string HoldReason, string HeldBy) : IRequest<Result>;
