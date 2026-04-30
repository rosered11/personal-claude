using MediatR;
using OMS.Application.Common.Models;

namespace OMS.Application.Orders.Commands.CancelOrder;

public record CancelOrderCommand(Guid OrderId, string Reason, string UpdatedBy) : IRequest<Result>;
