using MediatR;
using OMS.Application.Common.Models;

namespace OMS.Application.Orders.Commands.ReleaseOrder;

public record ReleaseOrderCommand(Guid OrderId, string ReleasedBy) : IRequest<Result>;
