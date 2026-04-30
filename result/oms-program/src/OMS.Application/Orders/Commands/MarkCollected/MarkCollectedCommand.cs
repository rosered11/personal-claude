using MediatR;
using OMS.Application.Common.Models;

namespace OMS.Application.Orders.Commands.MarkCollected;

public record MarkCollectedCommand(Guid OrderId, string UpdatedBy) : IRequest<Result>;
