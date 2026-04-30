using MediatR;
using OMS.Application.Common.Models;

namespace OMS.Application.Orders.Commands.StartPick;

public record StartPickCommand(Guid OrderId, string UpdatedBy) : IRequest<Result>;
